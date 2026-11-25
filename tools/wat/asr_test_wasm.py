"""Writes the test auto splitters in tests/fixtures (no wabt needed):
asr-test.wasm (the binary of asr-test.wat) and asr-widgets.wasm (asr-widgets.wat)."""
import pathlib


def uleb(n):
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        out.append(b | (0x80 if n else 0))
        if not n:
            return bytes(out)


def sleb(n):
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        done = (n == 0 and not b & 0x40) or (n == -1 and b & 0x40)
        out.append(b | (0 if done else 0x80))
        if done:
            return bytes(out)


def vec(items):
    return uleb(len(items)) + b"".join(items)


def name(s):
    return uleb(len(s)) + s.encode()


def section(i, body):
    return bytes([i]) + uleb(len(body)) + body


I32 = b"\x7F"
I64 = b"\x7E"
IF = b"\x04\x40"
END = b"\x0b"
DROP = b"\x1a"
GLOBAL_GET = b"\x23\x00"
GLOBAL_SET = b"\x24\x00"


def const(n):
    return b"\x41" + sleb(n)


def call(f):
    return b"\x10" + uleb(f)


def local_get(i):
    return b"\x20" + uleb(i)


def local_set(i):
    return b"\x21" + uleb(i)


class Module:
    """Imports from "env", two exported functions (_start, update), one memory, one i32 global."""

    def __init__(self):
        self.types = []
        self.imports = []
        self.strings = bytearray()

    def type(self, params, results):
        t = b"\x60" + vec(params) + vec(results)
        if t not in self.types:
            self.types.append(t)
        return self.types.index(t)

    def imp(self, field, params, results=()):
        self.imports.append(name("env") + name(field) + b"\x00" + uleb(self.type(list(params), list(results))))
        return len(self.imports) - 1

    def text(self, s):
        """The string's pointer and length, as two i32.const."""
        offset = len(self.strings)
        self.strings += s.encode()
        return const(offset) + const(len(s.encode()))

    def build(self, start, update, update_locals=()):
        void = self.type([], [])
        n = len(self.imports)
        functions = [uleb(void), uleb(void)]
        exports = [
            name("memory") + b"\x02" + uleb(0),
            name("_start") + b"\x00" + uleb(n),
            name("update") + b"\x00" + uleb(n + 1),
        ]

        def body(code, locals_=()):
            decl = vec([uleb(c) + t for c, t in locals_])
            return uleb(len(decl + code + END)) + decl + code + END

        code = [body(start), body(update, update_locals)]
        datas = [uleb(0) + const(0) + END + uleb(len(self.strings)) + bytes(self.strings)]
        return (b"\x00asm\x01\x00\x00\x00"
                + section(1, vec(self.types)) + section(2, vec(self.imports)) + section(3, vec(functions))
                + section(5, vec([b"\x00" + uleb(1)]))
                + section(6, vec([I32 + b"\x01" + const(0) + END]))
                + section(7, vec(exports))
                + section(10, vec(code)) + section(11, vec(datas)))


def asr_test():
    m = Module()
    start = m.imp("timer_start", [])
    state = m.imp("timer_get_state", [], [I32])
    add_bool = m.imp("user_settings_add_bool", [I32] * 5, [I32])
    set_var = m.imp("timer_set_variable", [I32] * 4)
    map_load = m.imp("settings_map_load", [], [I64])
    map_get = m.imp("settings_map_get", [I64, I32, I32], [I64])
    get_bool = m.imp("setting_value_get_bool", [I64, I32], [I32])
    value_free = m.imp("setting_value_free", [I64])
    map_free = m.imp("settings_map_free", [I64])
    key = m.text("start")
    m.strings += bytes(64 - len(m.strings))  # one byte at 64 for setting_value_get_bool
    scratch = const(64)
    m.strings += b"\x00"
    init = key + m.text("Start the run") + const(1) + call(add_bool) + GLOBAL_SET
    update = (
        m.text("phase") + m.text("idle") + call(set_var)
        + call(map_load) + local_set(0)
        + local_get(0) + key + call(map_get) + local_set(1)
        + local_get(1) + b"\x50" + b"\x45" + IF                      # the map has "start"
        + local_get(1) + scratch + call(get_bool) + IF               # and it is a bool
        + scratch + b"\x2d\x00\x00" + GLOBAL_SET + END               # i32.load8_u
        + local_get(1) + call(value_free) + END
        + local_get(0) + call(map_free)
        + GLOBAL_GET + IF + call(state) + b"\x45" + IF + call(start) + END + END
    )
    return m.build(init, update, [(2, I64)])


def asr_widgets():
    m = Module()
    add_title = m.imp("user_settings_add_title", [I32] * 5)
    add_bool = m.imp("user_settings_add_bool", [I32] * 5, [I32])
    add_choice = m.imp("user_settings_add_choice", [I32] * 6)
    add_option = m.imp("user_settings_add_choice_option", [I32] * 6, [I32])
    add_file = m.imp("user_settings_add_file_select", [I32] * 4)
    add_filter = m.imp("user_settings_add_file_select_name_filter", [I32] * 6)
    tooltip = m.imp("user_settings_set_tooltip", [I32] * 4)
    t = m.text
    init = (
        t("general") + t("General") + const(0) + call(add_title)
        + t("start") + t("Start the run") + const(1) + call(add_bool) + DROP
        + t("levels") + t("Split on levels") + const(0) + call(add_bool) + DROP
        + t("levels") + t("Splits when a level is finished") + call(tooltip)
        + t("advanced") + t("Advanced") + const(1) + call(add_title)
        + t("version") + t("Game version") + t("jp") + call(add_choice)
        + t("version") + t("jp") + t("Japanese") + call(add_option) + DROP
        + t("version") + t("us") + t("North American") + call(add_option) + DROP
        + t("save") + t("Select save file") + call(add_file)
        + t("save") + t("Save files") + t("*.sav") + call(add_filter)
    )
    return m.build(init, b"")


fixtures = pathlib.Path(__file__).resolve().parents[2] / "tests" / "fixtures"
for file, module in (("asr-test.wasm", asr_test()), ("asr-widgets.wasm", asr_widgets())):
    (fixtures / file).write_bytes(module)
    print(file, len(module), "bytes")
