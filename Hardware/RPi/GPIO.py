"""
RPi.GPIO stub / compatibility wrapper.
On Raspberry Pi Linux, this forwards to the real RPi.GPIO module.
On Windows / development machines, this provides a mock interface.
"""
import sys

_real_gpio = None
if sys.platform.startswith("linux"):
    try:
        import importlib
        import os
        this_dir = os.path.dirname(os.path.abspath(__file__))
        parent_dir = os.path.dirname(this_dir)
        orig_path = list(sys.path)
        sys.path = [p for p in sys.path if os.path.abspath(p) not in (this_dir, parent_dir)]
        try:
            _real_gpio = importlib.import_module("RPi.GPIO")
        finally:
            sys.path = orig_path
    except Exception:
        pass

if _real_gpio is not None:
    print("[RPi.GPIO] Native Linux GPIO loaded successfully.")
    BCM = _real_gpio.BCM
    BOARD = _real_gpio.BOARD
    OUT = _real_gpio.OUT
    IN = _real_gpio.IN
    HIGH = _real_gpio.HIGH
    LOW = _real_gpio.LOW
    PUD_UP = _real_gpio.PUD_UP
    PUD_DOWN = _real_gpio.PUD_DOWN

    setmode = _real_gpio.setmode
    setwarnings = _real_gpio.setwarnings
    setup = _real_gpio.setup
    output = _real_gpio.output
    input = _real_gpio.input
    cleanup = _real_gpio.cleanup
else:
    print("[RPi.GPIO] Running in simulation/mock mode.")
    # Windows / Development Mock
    BCM = 11
    BOARD = 10
    OUT = 0
    IN = 1
    HIGH = 1
    LOW = 0
    PUD_UP = 2
    PUD_DOWN = 1

    def setmode(mode):
        pass

    def setwarnings(flag):
        pass

    def setup(pin, mode, initial=None, pull_up_down=None):
        pass

    def output(pin, val):
        pass

    def input(pin):
        return 1

    def cleanup():
        pass
