"""
lgpio stub / compatibility wrapper.
On Raspberry Pi Linux, this forwards directly to the native Linux lgpio library.
On Windows / development machines, this provides a mock interface so the editor
resolves all symbols and app.py can run in dev/simulation mode without errors.
"""
import sys

_real_lgpio = None
if sys.platform.startswith("linux"):
    try:
        import importlib
        import os
        # Avoid importing this file recursively
        this_dir = os.path.dirname(os.path.abspath(__file__))
        orig_path = list(sys.path)
        sys.path = [p for p in sys.path if os.path.abspath(p) != this_dir]
        try:
            _real_lgpio = importlib.import_module("lgpio")
        finally:
            sys.path = orig_path
    except Exception:
        pass

if _real_lgpio is not None:
    print("[lgpio] Native Linux lgpio loaded successfully.")
    SET_PULL_UP = getattr(_real_lgpio, "SET_PULL_UP", 1)
    SET_PULL_DOWN = getattr(_real_lgpio, "SET_PULL_DOWN", 2)
    SET_PULL_NONE = getattr(_real_lgpio, "SET_PULL_NONE", 0)
    gpiochip_open = _real_lgpio.gpiochip_open
    gpio_get_chip_info = _real_lgpio.gpio_get_chip_info
    gpiochip_close = _real_lgpio.gpiochip_close
    gpio_claim_output = _real_lgpio.gpio_claim_output
    gpio_claim_input = _real_lgpio.gpio_claim_input
    gpio_write = _real_lgpio.gpio_write
    gpio_read = _real_lgpio.gpio_read
    gpio_free = _real_lgpio.gpio_free
else:
    print("[lgpio] Running in simulation/mock mode.")
    # Windows / Development Mock Interface
    SET_PULL_UP = 1
    SET_PULL_DOWN = 2
    SET_PULL_NONE = 0

    def gpiochip_open(number: int = 0) -> int:
        return 0

    def gpio_get_chip_info(handle: int = 0):
        return [40, "pinctrl-rp1", "gpiochip0", "pinctrl-rp1"]

    def gpiochip_close(handle: int) -> None:
        pass

    def gpio_claim_output(chip: int, pin: int, level: int = 0) -> None:
        pass

    def gpio_claim_input(chip: int, pin: int, pull: int = 0) -> None:
        pass

    def gpio_write(chip: int, pin: int, value: int) -> None:
        pass

    def gpio_read(chip: int, pin: int) -> int:
        return 1

    def gpio_free(chip: int, pin: int) -> None:
        pass
