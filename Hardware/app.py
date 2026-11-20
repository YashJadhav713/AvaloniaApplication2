from flask import Flask, request, jsonify
import RPi.GPIO as GPIO
import threading
import time
import subprocess
import socket
import json
import os
import math
import lgpio


GPIO.setmode(GPIO.BCM)
GPIO.setwarnings(False)

# All relay outputs are active low
ON = GPIO.LOW
OFF = GPIO.HIGH


# ---------------- Output pins (direct) ----------------

TOGGLE_RELAY = 16
GLASS_DISPENSER = 6
DOOR_REV = 7
DOOR_FWD = 8
ACT_REV = 11
ACT_FWD = 25
CHAMBER_LED = 19

DIRECT_OUTPUTS = [
    TOGGLE_RELAY, GLASS_DISPENSER, DOOR_REV, DOOR_FWD,
    ACT_REV, ACT_FWD, CHAMBER_LED,
]


# ---------------- Output pins (shared, via toggle) ----------------
# Toggle ON : 9, 10, 24, 23 = Canister 1-4, 22 = Pump 1 (suction),
#             27 = Pump 2 (main), 18 = Blender, 17 = Hot air
# Toggle OFF: 9, 10, 24, 23 = Canister 5-8

TOGGLE_ON = GPIO.LOW
TOGGLE_OFF = GPIO.HIGH

SHARED_PINS = [9, 10, 24, 23, 22, 27, 18, 17]

CANISTERS = {
    1: (9, TOGGLE_ON),
    2: (10, TOGGLE_ON),
    3: (24, TOGGLE_ON),
    4: (23, TOGGLE_ON),
    5: (9, TOGGLE_OFF),
    6: (10, TOGGLE_OFF),
    7: (24, TOGGLE_OFF),
    8: (23, TOGGLE_OFF),
}

SUCTION_PUMP = 22
WATER_PUMP = 27
BLENDER = 18
HOT_AIR = 17

TOGGLE_SETTLE_TIME = 0.5


# ---------------- Input pins ----------------

LIMIT_SW_1 = 13
GLASS_SENSOR = 12
DOOR_SWITCH = 15
SUCTION_BUTTON = 4


# ---------------- Stepper pins ----------------

STEPPER_EN = 26
STEPPER_DIR = 20
STEPPER_STEP = 21



def find_header_chip():
    # The 40-pin header chip number depends on the Pi model and kernel:
    #   Pi 5, new kernel : gpiochip0 (pinctrl-rp1)
    #   Pi 5, old kernel : gpiochip4 (pinctrl-rp1)
    #   Pi 4 / 3         : gpiochip0 (pinctrl-bcm2711 / bcm2835)
    # So find it by its label instead of hard-coding the number.

    for number in range(16):
        try:
            handle = lgpio.gpiochip_open(number)
        except Exception:
            continue
        try:
            info = lgpio.gpio_get_chip_info(handle)
            label = info[3] if isinstance(info, (list, tuple)) else ""
            if "rp1" in label or "pinctrl-bcm" in label:
                print(f"GPIO header chip = gpiochip{number} ({label})")
                return number
        except Exception:
            pass
        finally:
            lgpio.gpiochip_close(handle)

    print("GPIO header chip not detected, using gpiochip0")
    return 0


GPIO_CHIP = find_header_chip()


def gpio_init():
    for pin in DIRECT_OUTPUTS + SHARED_PINS:
        GPIO.setup(pin, GPIO.OUT, initial=OFF)

    GPIO.setup(DOOR_SWITCH, GPIO.IN, pull_up_down=GPIO.PUD_UP)
    GPIO.setup(GLASS_SENSOR, GPIO.IN, pull_up_down=GPIO.PUD_UP)
    GPIO.setup(SUCTION_BUTTON, GPIO.IN, pull_up_down=GPIO.PUD_UP)

    print("GPIO Initialized")


gpio_init()


# ---------------- Shared bank manager ----------------

bank_cond = threading.Condition()
bank_users = 0
toggle_state = TOGGLE_OFF


def shared_on(pin, bank):
    global bank_users
    global toggle_state

    with bank_cond:
        while bank_users > 0 and toggle_state != bank:
            bank_cond.wait()

        if toggle_state != bank:
            for p in SHARED_PINS:
                GPIO.output(p, OFF)

            GPIO.output(TOGGLE_RELAY, bank)
            toggle_state = bank

            print("Toggle -> " + ("ON" if bank == TOGGLE_ON else "OFF"))
            time.sleep(TOGGLE_SETTLE_TIME)

        bank_users += 1
        GPIO.output(pin, ON)


def shared_off(pin):
    global bank_users

    with bank_cond:
        GPIO.output(pin, OFF)
        bank_users -= 1
        bank_cond.notify_all()


def run_shared(pin, bank, run_time):
    shared_on(pin, bank)
    try:
        time.sleep(run_time)
    finally:
        shared_off(pin)


def toggle_idle():
    global toggle_state

    with bank_cond:
        if bank_users > 0:
            return

        for p in SHARED_PINS:
            GPIO.output(p, OFF)

        if toggle_state != TOGGLE_OFF:
            GPIO.output(TOGGLE_RELAY, TOGGLE_OFF)
            toggle_state = TOGGLE_OFF
            print("Toggle -> OFF (idle)")


# ---------------- Stepper (lgpio) ----------------

ENABLE_ON = 0
ENABLE_OFF = 1

CLOCKWISE = 1
COUNTERCLOCKWISE = 0

STEPPER_HOME_DIR = COUNTERCLOCKWISE
STEPPER_AWAY_DIR = CLOCKWISE

chip = None


def stepper_init():
    global chip

    chip = lgpio.gpiochip_open(GPIO_CHIP)

    lgpio.gpio_claim_output(chip, STEPPER_STEP, 0)
    lgpio.gpio_claim_output(chip, STEPPER_DIR, 0)
    lgpio.gpio_claim_output(chip, STEPPER_EN, ENABLE_OFF)

    # Limit switch: read through lgpio, internal pull-up, LOW = home
    lgpio.gpio_claim_input(chip, LIMIT_SW_1, lgpio.SET_PULL_UP)

    print("Stepper (lgpio) Initialized")


def stepper_cleanup():
    if chip is None:
        return

    try:
        lgpio.gpio_write(chip, STEPPER_STEP, 0)
        lgpio.gpio_write(chip, STEPPER_EN, ENABLE_OFF)

        lgpio.gpio_free(chip, STEPPER_STEP)
        lgpio.gpio_free(chip, STEPPER_DIR)
        lgpio.gpio_free(chip, STEPPER_EN)
        lgpio.gpio_free(chip, LIMIT_SW_1)

        lgpio.gpiochip_close(chip)
    except Exception:
        pass


stepper_init()


# ---------------- Machine status ----------------

DOOR_CLOSE = "B14"
DOOR_OPEN = "B15"

STATUS_IDLE = "B10"            # order complete, machine idle
STATUS_BUSY = "B6"
STATUS_GLASS_FAILED = "B3"
STATUS_PICKUP_WAIT = "B20"     # LED started blinking, waiting for pickup
STATUS_PICKED_UP = "B21"       # glass picked up and confirmed

bits = STATUS_IDLE
temp = ""

cycle_running = False

# Glass result shared with /drinktime
# None = not known yet, "ok" = glass detected, "fail" = no glass
glass_state = None
drink_time_value = 0
glass_event = threading.Event()

# How long /drinktime waits for the glass result (seconds).
DRINKTIME_WAIT_TIMEOUT = 15


# ---------------- Hot air control ----------------

HOT_AIR_WAIT = 300
HOT_AIR_TIME = 5

hot_air_running = False
hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

machine_lock = threading.Lock()
hot_air_wakeup = threading.Event()


# ---------------- Timings (seconds) ----------------

STARTUP_DOOR_CLOSE_TIME = 22
STARTUP_ACT_REV_TIME = 20

DOOR_OPEN_TIME = 25
DOOR_CLOSE_TIME = 25

ACT_FWD_TIME_1 = 6
ACT_FWD_TIME_2 = 14
ACT_REV_TIME_1 = 12
ACT_REV_TIME_2 = 22

BLENDER_TIME_1 = 20
BLENDER_TIME_2 = 5

# Start points inside ACT FWD 2
PUMP_START_FRACTION = 1 / 3
BLENDER_1_START_FRACTION = 2 / 3

PUMP_CLEAN_TIME = 4

GLASS_DISPENSER_TIME = 3
GLASS_CHECK_DELAY = 1
GLASS_SLIDE_TIME = 2
GLASS_ATTEMPTS = 2

GLASS_SENSOR_DEBOUNCE = 0.5
PICKUP_DELAY = 3

SUCTION_BUTTON_HOLD = 3
SUCTION_RUN_TIME = 300


# ---------------- Recipes (8 menus) ----------------

DEFAULT_POWDER = 9
DEFAULT_WATER = 30

POWDER_CALIBRATION = {m: DEFAULT_POWDER for m in range(1, 9)}
WATER_CALIBRATION = {m: DEFAULT_WATER for m in range(1, 9)}

CALIBRATION_FILE = "calibration.json"

# Index 0-7 = Powder 1-8, index 8-15 = Water 1-8
CALIBRATION_COUNT = 16


# ---------------- Stepper settings ----------------

STEP_PULSE = 0.00001
STEP_DELAY = 0.00005

# Limit switch is active LOW.
LIMIT_ACTIVE = 0

LIMIT_SAMPLE_INTERVAL = 0.002
LIMIT_SAMPLES_NEEDED = 4
LIMIT_QUICK_READS = 8
LIMIT_CONFIRM_SAMPLES = 25
LIMIT_CONFIRM_INTERVAL = 0.002

MENU_STEPS = {
    1: 6500,
    2: 24000,
    3: 40000,
    4: 55500,
    5: 72000,
    6: 89000,
    7: 0,
    8: 0,
}

BLENDER_STEPS = {
    1: 82500,
    2: 65000,
    3: 49000,
    4: 33500,
    5: 17000,
    6: 0,
    7: 0,
    8: 0,
}

BLENDING_REACH_STEPS = 12000
HOME_MARGIN_STEPS = 2000

MAX_TRAVEL_STEPS = max(
    MENU_STEPS[m] + BLENDER_STEPS[m] for m in MENU_STEPS
) + BLENDING_REACH_STEPS

MAX_HOME_STEPS = MAX_TRAVEL_STEPS + HOME_MARGIN_STEPS

print(
    f"Max travel = {MAX_TRAVEL_STEPS} steps, "
    f"homing limit = {MAX_HOME_STEPS} steps"
)


# ---------------- Helpers ----------------

def run_pin(pin, run_time):
    GPIO.output(pin, ON)
    time.sleep(run_time)
    GPIO.output(pin, OFF)


def run_water_pump(run_time, label="Water"):
    shared_on(SUCTION_PUMP, TOGGLE_ON)
    shared_on(WATER_PUMP, TOGGLE_ON)

    print(f"{label} Pump + Suction ON ({run_time} s)")
    try:
        time.sleep(run_time)
    finally:
        shared_off(WATER_PUMP)
        shared_off(SUCTION_PUMP)

    print(f"{label} Pump + Suction OFF")


def machine_on_led():
    GPIO.output(CHAMBER_LED, ON)
    time.sleep(2)
    GPIO.output(CHAMBER_LED, OFF)


def flask_ready_led():
    for _ in range(2):
        GPIO.output(CHAMBER_LED, ON)
        time.sleep(0.5)
        GPIO.output(CHAMBER_LED, OFF)
        time.sleep(0.5)


def run_motor(forward_pin, reverse_pin, direction, run_time):
    if direction == "FWD":
        run_pin(forward_pin, run_time)
    elif direction == "REV":
        run_pin(reverse_pin, run_time)
    else:
        print("Invalid motor direction")


def release_machine():
    global cycle_running
    global hot_air_deadline

    with machine_lock:
        cycle_running = False
        hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

    hot_air_wakeup.set()


# ---------------- Hot air ----------------

def reset_hot_air_timer():
    global hot_air_deadline

    with machine_lock:
        hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

    hot_air_wakeup.set()


def hot_air_thread():
    global hot_air_running
    global hot_air_deadline

    print("Hot Air Thread Started")

    while True:
        while True:
            with machine_lock:
                remaining = hot_air_deadline - time.monotonic()
                if remaining <= 0:
                    break
                if cycle_running:
                    wait_time = 0.5
                else:
                    wait_time = remaining

            hot_air_wakeup.wait(wait_time)
            hot_air_wakeup.clear()

        with machine_lock:
            if cycle_running:
                hot_air_deadline = time.monotonic() + HOT_AIR_WAIT
                continue
            hot_air_running = True

        print("Automatic Hot Air ON")
        run_shared(HOT_AIR, TOGGLE_ON, HOT_AIR_TIME)
        toggle_idle()

        with machine_lock:
            hot_air_running = False
            hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

        print("Automatic Hot Air OFF")


def wait_for_hot_air():
    while True:
        with machine_lock:
            if not hot_air_running:
                return
        print("Hot Air running - waiting...")
        time.sleep(0.1)


def run_manual_hot_air():
    global hot_air_running
    global hot_air_deadline

    with machine_lock:
        hot_air_running = True

    print("Hot Air ON")
    run_shared(HOT_AIR, TOGGLE_ON, HOT_AIR_TIME)

    with machine_lock:
        hot_air_running = False
        hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

    print("Hot Air OFF")
    hot_air_wakeup.set()


# ---------------- Suction button ----------------

def suction_only_run():
    print(f"Suction Pump ON (Button, {SUCTION_RUN_TIME} s)")
    try:
        run_shared(SUCTION_PUMP, TOGGLE_ON, SUCTION_RUN_TIME)
    finally:
        toggle_idle()
        print("Suction Pump OFF (Button)")
        release_machine()


def start_suction_from_button():
    global cycle_running
    global hot_air_deadline

    with machine_lock:
        if cycle_running:
            print("Suction Button Ignored (Machine Busy)")
            return
        if hot_air_running:
            print("Suction Button Ignored (Hot Air Running)")
            return
        cycle_running = True
        hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

    hot_air_wakeup.set()
    threading.Thread(target=suction_only_run, daemon=True).start()


def suction_button_monitor():
    print("Suction Button Monitor Started")
    while True:
        if GPIO.input(SUCTION_BUTTON) == GPIO.LOW:
            press_start = time.monotonic()
            triggered = False
            while GPIO.input(SUCTION_BUTTON) == GPIO.LOW:
                if (
                    not triggered
                    and time.monotonic() - press_start
                    >= SUCTION_BUTTON_HOLD
                ):
                    print("Suction Button Held 3 s")
                    start_suction_from_button()
                    triggered = True
                time.sleep(0.05)
        time.sleep(0.05)


# ---------------- Stepper motion ----------------

def stepper_move(steps, direction):
    if steps <= 0:
        return

    try:
        lgpio.gpio_write(chip, STEPPER_STEP, 0)
        lgpio.gpio_write(chip, STEPPER_EN, ENABLE_ON)
        time.sleep(0.05)
        lgpio.gpio_write(chip, STEPPER_DIR, direction)
        time.sleep(0.01)

        for _ in range(steps):
            lgpio.gpio_write(chip, STEPPER_STEP, 1)
            time.sleep(STEP_PULSE)
            lgpio.gpio_write(chip, STEPPER_STEP, 0)
            time.sleep(STEP_DELAY)

    finally:
        lgpio.gpio_write(chip, STEPPER_STEP, 0)
        lgpio.gpio_write(chip, STEPPER_EN, ENABLE_OFF)


def limit_active():
    return lgpio.gpio_read(chip, LIMIT_SW_1) == LIMIT_ACTIVE


def limit_confirmed():
    for _ in range(LIMIT_QUICK_READS):
        if not limit_active():
            return False

    for _ in range(LIMIT_CONFIRM_SAMPLES):
        time.sleep(LIMIT_CONFIRM_INTERVAL)
        if not limit_active():
            return False

    return True


def limit_pressed():
    return limit_confirmed()


def go_home():
    print("Stepper Homing...")
    if limit_pressed():
        print("Stepper Already At Home (No Movement)")
        return True

    step = 0
    homed = False

    try:
        lgpio.gpio_write(chip, STEPPER_STEP, 0)
        lgpio.gpio_write(chip, STEPPER_EN, ENABLE_ON)
        time.sleep(0.05)
        lgpio.gpio_write(chip, STEPPER_DIR, STEPPER_HOME_DIR)
        time.sleep(0.01)

        last_sample = time.monotonic()
        low_samples = 0

        while step < MAX_HOME_STEPS:
            now = time.monotonic()
            if now - last_sample >= LIMIT_SAMPLE_INTERVAL:
                last_sample = now
                if limit_active():
                    low_samples += 1
                    if low_samples >= LIMIT_SAMPLES_NEEDED:
                        if limit_confirmed():
                            homed = True
                            break
                        low_samples = 0
                else:
                    low_samples = 0

            lgpio.gpio_write(chip, STEPPER_STEP, 1)
            time.sleep(STEP_PULSE)
            lgpio.gpio_write(chip, STEPPER_STEP, 0)
            time.sleep(STEP_DELAY)
            step += 1

    finally:
        lgpio.gpio_write(chip, STEPPER_STEP, 0)
        lgpio.gpio_write(chip, STEPPER_EN, ENABLE_OFF)

    if homed:
        print(f"Stepper Home Complete ({step} steps)")
    else:
        print(f"Home Failed (Step Limit {MAX_HOME_STEPS} Reached)")

    return homed


def go_menu(menu):
    if menu not in MENU_STEPS:
        print("Invalid Menu Steps")
        return

    print(f"Moving To Menu {menu}")
    stepper_move(MENU_STEPS[menu], STEPPER_AWAY_DIR)
    print("Menu Position Reached")


def go_blender(menu):
    if menu not in BLENDER_STEPS:
        print("Invalid Blender Steps")
        return

    print(f"Moving To Blender (Menu {menu})")
    stepper_move(BLENDER_STEPS[menu], STEPPER_AWAY_DIR)
    print("Blender Position Reached")


def go_blending_reach():
    print("Moving To Blending Reach")
    stepper_move(BLENDING_REACH_STEPS, STEPPER_AWAY_DIR)
    print("Blending Reach Position Reached")


# ---------------- Glass dispense ----------------

def dispense_glass():
    print("Glass Dispensing...")

    for attempt in range(1, GLASS_ATTEMPTS + 1):
        run_pin(GLASS_DISPENSER, GLASS_DISPENSER_TIME)
        time.sleep(GLASS_CHECK_DELAY)

        if GPIO.input(GLASS_SENSOR) == 0:
            print(f"Glass Detected (Attempt {attempt})")
            time.sleep(GLASS_SLIDE_TIME)

            if GPIO.input(GLASS_SENSOR) == 0:
                print("Glass Confirmed")
                return True

            print("Glass Lost After Sliding")

        print(f"Glass Attempt {attempt} Failed")

    print("Glass Dispense Failed")
    return False


# ---------------- Drink time estimate ----------------

STEP_OVERHEAD = 0.00007
STEPPER_SETUP_TIME = 0.06
DRINK_TIME_MARGIN = 0


def step_move_time(steps):
    if steps <= 0:
        return 0.0

    return STEPPER_SETUP_TIME + steps * (
        STEP_PULSE + STEP_DELAY + STEP_OVERHEAD
    )


def estimate_drink_time(menu):
    canister_pin, canister_bank = CANISTERS[menu]

    powder_time = POWDER_CALIBRATION.get(menu, DEFAULT_POWDER)
    water_time = WATER_CALIBRATION.get(menu, DEFAULT_WATER)

    powder_settle = (
        TOGGLE_SETTLE_TIME if canister_bank != TOGGLE_OFF else 0
    )
    pump_settle = (
        TOGGLE_SETTLE_TIME if canister_bank != TOGGLE_ON else 0
    )

    stage1 = (
        step_move_time(MENU_STEPS[menu])
        + powder_settle
        + powder_time
        + 1
        + step_move_time(BLENDER_STEPS[menu])
    )

    t = max(stage1, ACT_FWD_TIME_1)
    t += step_move_time(BLENDING_REACH_STEPS)

    pump_start = ACT_FWD_TIME_2 * PUMP_START_FRACTION
    pump_end = pump_start + pump_settle + water_time

    blender1_end = (
        ACT_FWD_TIME_2 * BLENDER_1_START_FRACTION + BLENDER_TIME_1
    )

    stage3 = max(ACT_FWD_TIME_2, blender1_end)
    stage3 += ACT_REV_TIME_1
    stage3 = max(stage3, pump_end)
    stage3 += max(BLENDER_TIME_2, PUMP_CLEAN_TIME)
    stage3 += ACT_REV_TIME_2

    return t + stage3 + DRINK_TIME_MARGIN


# ---------------- Startup ----------------

machine_on_led()
print("Startup: Stepper HOME")
go_home()
time.sleep(1)
print("Startup: Closing Chamber Door...")
run_motor(DOOR_FWD, DOOR_REV, "FWD", STARTUP_DOOR_CLOSE_TIME)
print(f"Startup: ACT REV ({STARTUP_ACT_REV_TIME} s)...")
run_motor(ACT_FWD, ACT_REV, "REV", STARTUP_ACT_REV_TIME)
print("Machine Ready")


# ---------------- WiFi ----------------

def is_internet_available():
    try:
        socket.create_connection(("8.8.8.8", 53), timeout=5)
        return True
    except:
        return False


def connect_wifi(ssid, password, retries=6):
    for attempt in range(1, retries + 1):
        print(f"Connecting WiFi (Attempt {attempt})")
        try:
            subprocess.run(["nmcli", "radio", "wifi", "on"], check=False)
            subprocess.run(["nmcli", "device", "disconnect", "wlan0"])
            subprocess.run(["nmcli", "connection", "delete", ssid])
            subprocess.run(
                [
                    "nmcli", "connection", "add",
                    "type", "wifi",
                    "ifname", "wlan0",
                    "con-name", ssid,
                    "ssid", ssid
                ],
                check=True
            )
            subprocess.run(
                [
                    "nmcli", "connection", "modify", ssid,
                    "wifi-sec.key-mgmt", "wpa-psk",
                    "wifi-sec.psk", password
                ],
                check=True
            )
            subprocess.run(
                ["nmcli", "connection", "up", ssid],
                check=True
            )
            time.sleep(8)
            if is_internet_available():
                print("WiFi Connected")
                return True
        except:
            print("WiFi Connection Failed")
        time.sleep(5)
    return False


# ---------------- Packet decoder ----------------

def decode_packet(bits_tuple):
    menu = None
    for i in range(8):
        if bits_tuple[i] == 1:
            menu = i + 1
            break

    # Protein Bar 1 -> Bit 9 (Index 8)
    # Protein Bar 2 -> Bit 10 (Index 9)
    if menu is None:
        if len(bits_tuple) > 8 and bits_tuple[8] == 1:
            menu = 9   # Protein Bar 1
        elif len(bits_tuple) > 9 and bits_tuple[9] == 1:
            menu = 10  # Protein Bar 2

    if menu is None:
        print("No Menu Selected")
        return None

    liquid = "WATER" if (len(bits_tuple) <= 10 or bits_tuple[10] == 0) else "MILK"
    if menu in (9, 10):
        print(f"Protein Bar : {menu - 8} (ID {menu})")
    else:
        print(f"Menu   : {menu}")
        print(f"Liquid : {liquid}")
    return menu, liquid


# ---------------- Flask server ----------------

app = Flask(__name__)


@app.route("/")
def home():
    return "B11", 200


@app.route("/config")
def config():
    ssid = request.args.get("ssid")
    password = request.args.get("password")
    if connect_wifi(ssid, password):
        return "B2", 200
    return "B4", 200


@app.route("/getbits")
def get_bits():
    global bits
    return jsonify(bits), 200


@app.route("/sendbits")
def send_bits():
    global cycle_running
    global hot_air_deadline
    global glass_state

    data = request.args.get("bits")
    if not data:
        return "INVALID", 400

    print("Packet Received:", data)
    try:
        bits_tuple = tuple(
            map(int, data.strip("()").replace(" ", "").split(","))
        )
    except:
        print("Packet Parse Failed")
        return "INVALID", 400

    try:
        packet = decode_packet(bits_tuple)
    except IndexError:
        print("Packet Too Short")
        return "INVALID", 400

    if packet is None:
        return "INVALID", 400

    if packet[0] not in CANISTERS and packet[0] not in (9, 10):
        print("Invalid Menu Number")
        return "INVALID", 400

    with machine_lock:
        if cycle_running:
            print("Machine Busy")
            return "BUSY", 200

        cycle_running = True
        bits = STATUS_BUSY
        hot_air_deadline = time.monotonic() + HOT_AIR_WAIT

        # New order: forget previous glass result
        glass_state = None
        glass_event.clear()

    hot_air_wakeup.set()

    if packet[0] in (9, 10):
        threading.Thread(
            target=bar_cycle,
            args=(packet[0],),
            daemon=True
        ).start()
    else:
        threading.Thread(
            target=psvm_cycle,
            args=(packet,),
            daemon=True
        ).start()

    return "RECEIVED", 200


@app.route("/drinktime")
def drink_time():
    # No order running and no result: nothing to answer
    if glass_state is None and not cycle_running:
        return "INVALID", 400

    # Wait for the glass result (homing + glass attempts take time)
    # Returns B6 if not ready yet
    if not glass_event.wait(DRINKTIME_WAIT_TIMEOUT):
        print("DrinkTime: glass result not ready (B6)")
        return STATUS_BUSY, 200

    if glass_state == "fail":
        return STATUS_GLASS_FAILED, 200

    return f"T{drink_time_value}", 200


@app.route("/getcalibration")
def get_calibration():
    return jsonify({
        f"Powder{m}": POWDER_CALIBRATION.get(m, DEFAULT_POWDER)
        for m in range(1, 9)
    }), 200


@app.route("/door")
def door():
    if GPIO.input(DOOR_SWITCH) == GPIO.LOW:
        return DOOR_OPEN, 200
    return DOOR_CLOSE, 200


@app.route("/calibration")
def calibration():
    time_data = request.args.get("time")
    if not time_data:
        print("Calibration data missing")
        return "INVALID", 400

    try:
        data = json.loads(time_data)
        if not isinstance(data, list):
            print("Calibration data is not an array")
            return "INVALID", 400

        if len(data) != CALIBRATION_COUNT:
            print(
                f"Calibration array length = {len(data)}, "
                f"expected {CALIBRATION_COUNT}"
            )
            return "INVALID", 400

        calibration_data = [float(value) for value in data]
        for value in calibration_data:
            if value <= 0:
                print("Invalid calibration value:", value)
                return "INVALID", 400

        with open(CALIBRATION_FILE, "w") as f:
            json.dump(calibration_data, f, indent=4)

        print("Calibration Received")
        for index, value in enumerate(calibration_data):
            print(f"Index {index} = {value}")

        load_calibration()
        return "OK", 200

    except Exception as e:
        print("Calibration Error:", e)
        return "INVALID", 400


# ---------------- Load calibration ----------------

def load_calibration():
    if not os.path.exists(CALIBRATION_FILE):
        print("No calibration file. Using default values.")
        return

    try:
        with open(CALIBRATION_FILE, "r") as f:
            data = json.load(f)

        if not isinstance(data, list):
            print("Calibration data is not an array. Using defaults.")
            return

        if len(data) < CALIBRATION_COUNT:
            print(
                f"Calibration has {len(data)} values, "
                f"need {CALIBRATION_COUNT}. Using defaults."
            )
            return

        for menu in range(1, 9):
            for table, index, name in (
                (POWDER_CALIBRATION, menu - 1, "Powder"),
                (WATER_CALIBRATION, menu + 7, "Water"),
            ):
                try:
                    value = float(data[index])
                    if value > 0:
                        table[menu] = value
                        print(f"{name} {menu} [index {index}] = {value}")
                    else:
                        print(
                            f"{name} {menu} [index {index}] "
                            f"invalid. Using default."
                        )
                except:
                    print(
                        f"{name} {menu} [index {index}] "
                        f"invalid. Using default."
                    )
    except Exception as e:
        print("Calibration file error:", e)
        print("Using default values.")


# ---------------- Main drink cycle ----------------

def psvm_cycle(packet):
    global bits
    global glass_state
    global drink_time_value

    try:
        wait_for_hot_air()
        menu, liquid = packet

        print("Drink Cycle Started")
        print(f"Menu   : {menu}")
        print(f"Liquid : {liquid}")

        bits = STATUS_BUSY

        if not limit_pressed():
            go_home()

        if not dispense_glass():
            glass_state = "fail"
            glass_event.set()
            bits = STATUS_GLASS_FAILED
            release_machine()
            print("Drink Cycle Aborted (No Glass)")
            return

        # Glass OK: estimate the time from stepper start to LED blink
        drink_time_value = int(math.ceil(estimate_drink_time(menu)))
        glass_state = "ok"
        glass_event.set()
        print(f"Drink Time Estimate = {drink_time_value} s")

        # Powder: actuator FWD 1 runs while moving to the canister
        act_thread = threading.Thread(
            target=run_motor,
            args=(ACT_FWD, ACT_REV, "FWD", ACT_FWD_TIME_1)
        )
        act_thread.start()

        go_menu(menu)
        powder_time = POWDER_CALIBRATION.get(menu, DEFAULT_POWDER)
        canister_pin, canister_bank = CANISTERS[menu]

        print(f"Powder Running (Menu {menu}, {powder_time} s)")
        run_shared(canister_pin, canister_bank, powder_time)
        time.sleep(1)

        go_blender(menu)
        act_thread.join()
        print("ACT FWD 1 Complete")

        go_blending_reach()
        water_total_time = WATER_CALIBRATION.get(menu, DEFAULT_WATER)
        print(f"Water (Menu {menu}) = {water_total_time} s")

        # ACT FWD 2 + pump + blender 1
        pump_delay = ACT_FWD_TIME_2 * PUMP_START_FRACTION
        blender_delay = ACT_FWD_TIME_2 * BLENDER_1_START_FRACTION

        print(f"ACT FWD 2 ({ACT_FWD_TIME_2} s)")
        act2_thread = threading.Thread(
            target=run_motor,
            args=(ACT_FWD, ACT_REV, "FWD", ACT_FWD_TIME_2)
        )

        def delayed_pump():
            time.sleep(pump_delay)
            print(f"Pump Start at {round(pump_delay, 2)} s of ACT FWD 2")
            run_water_pump(water_total_time, "Water")

        def blender_1():
            time.sleep(blender_delay)
            print(f"Blender 1 ON ({BLENDER_TIME_1} s)")
            run_shared(BLENDER, TOGGLE_ON, BLENDER_TIME_1)
            print("Blender 1 OFF")

        pump_thread = threading.Thread(target=delayed_pump)
        blender1_thread = threading.Thread(target=blender_1)

        act2_thread.start()
        pump_thread.start()
        blender1_thread.start()

        act2_thread.join()
        print("ACT FWD 2 Complete")

        blender1_thread.join()
        print("ACT REV 1")
        run_motor(ACT_FWD, ACT_REV, "REV", ACT_REV_TIME_1)
        pump_thread.join()

        # Door opens together with blender 2 + pump rinse
        def door_open():
            print("Door Opening")
            run_motor(DOOR_FWD, DOOR_REV, "REV", DOOR_OPEN_TIME)
            print("Door Fully Open")

        door_open_thread = threading.Thread(target=door_open)
        door_open_thread.start()

        print("Blender 2 + Pump Clean ON")
        blender_thread = threading.Thread(
            target=run_shared,
            args=(BLENDER, TOGGLE_ON, BLENDER_TIME_2)
        )
        clean_pump_thread = threading.Thread(
            target=run_water_pump,
            args=(PUMP_CLEAN_TIME, "Clean")
        )

        blender_thread.start()
        clean_pump_thread.start()
        blender_thread.join()
        clean_pump_thread.join()

        print("Blender 2 + Pump Clean OFF")
        print("ACT REV 2")
        run_motor(ACT_FWD, ACT_REV, "REV", ACT_REV_TIME_2)
        print("ACT REV 2 Complete")

        # Pickup: LED blinks until the glass is removed
        print("Waiting For Pickup (LED Blinking)")
        bits = STATUS_PICKUP_WAIT          # B20

        while GPIO.input(GLASS_SENSOR) == 0:
            GPIO.output(CHAMBER_LED, ON)
            time.sleep(0.5)
            GPIO.output(CHAMBER_LED, OFF)
            time.sleep(0.5)

        debounce_start = time.monotonic()
        while time.monotonic() - debounce_start < GLASS_SENSOR_DEBOUNCE:
            if GPIO.input(GLASS_SENSOR) == 0:
                debounce_start = time.monotonic()
            time.sleep(0.01)

        GPIO.output(CHAMBER_LED, OFF)
        bits = STATUS_PICKED_UP            # B21
        print("Glass Picked Up")

        time.sleep(PICKUP_DELAY)

        # Door must finish opening before it closes
        door_open_thread.join()

        stepper_thread = threading.Thread(target=go_home)
        door_thread = threading.Thread(
            target=run_motor,
            args=(DOOR_FWD, DOOR_REV, "FWD", DOOR_CLOSE_TIME)
        )

        stepper_thread.start()
        door_thread.start()
        stepper_thread.join()

        run_manual_hot_air()
        door_thread.join()

        print("Door Fully Closed")
        toggle_idle()
        release_machine()

        bits = STATUS_IDLE                 # B10, machine is idle now
        time.sleep(1)
        print("Drink Cycle Complete")

    except Exception as e:
        print(f"Exception during drink cycle: {e}")
        glass_state = "fail"
        glass_event.set()
        bits = STATUS_GLASS_FAILED
        release_machine()


# ---------------- Protein bar cycle ----------------

def bar_cycle(bar_id):
    global bits
    global glass_state

    try:
        wait_for_hot_air()
        print(f"Bar Cycle Started: Protein Bar {bar_id - 8} (ID: {bar_id})")
        bits = STATUS_BUSY

        # 10s pickup window for protein bar 1 or bar 2
        bits = STATUS_PICKUP_WAIT  # B20
        pickup_timeout = 10.0      # Give 10s to pick up bar
        pickup_start = time.monotonic()

        while time.monotonic() - pickup_start < pickup_timeout:
            GPIO.output(CHAMBER_LED, ON)
            time.sleep(0.5)
            GPIO.output(CHAMBER_LED, OFF)
            time.sleep(0.5)

            if GPIO.input(GLASS_SENSOR) != 0:
                print("Bar Picked Up via Sensor")
                break

        GPIO.output(CHAMBER_LED, OFF)
        bits = STATUS_PICKED_UP    # B21
        print("Bar Picked Up or Timeout Complete")
        time.sleep(0.5)

        release_machine()
        bits = STATUS_IDLE         # B10
        print("Bar Cycle Complete")
    except Exception as e:
        print(f"Exception during bar cycle: {e}")
        release_machine()
        bits = STATUS_IDLE


load_calibration()


# ---------------- Start server ----------------

if __name__ == "__main__":
    threading.Thread(target=hot_air_thread, daemon=True).start()
    threading.Thread(target=flask_ready_led, daemon=True).start()
    threading.Thread(target=suction_button_monitor, daemon=True).start()

    try:
        app.run(host="0.0.0.0", port=5000, threaded=True)
    finally:
        toggle_idle()
        stepper_cleanup()
