;; A minimal WebAssembly auto splitter for the Auto Splitting Runtime tests
;; (hand-assembled into asr-test.wasm by tools/wat/asr_test_wasm.py, which lays the strings out
;; one after another instead of at these addresses).
(module
  (import "env" "timer_start" (func $start))
  (import "env" "timer_get_state" (func $state (result i32)))
  (import "env" "user_settings_add_bool" (func $add_bool (param i32 i32 i32 i32 i32) (result i32)))
  (import "env" "timer_set_variable" (func $set_var (param i32 i32 i32 i32)))
  (import "env" "settings_map_load" (func $map_load (result i64)))
  (import "env" "settings_map_get" (func $map_get (param i64 i32 i32) (result i64)))
  (import "env" "setting_value_get_bool" (func $get_bool (param i64 i32) (result i32)))
  (import "env" "setting_value_free" (func $value_free (param i64)))
  (import "env" "settings_map_free" (func $map_free (param i64)))
  (memory (export "memory") 1)
  (global $enabled (mut i32) (i32.const 0))
  (data (i32.const 0) "start")
  (data (i32.const 16) "Start the run")
  (data (i32.const 32) "phase")
  (data (i32.const 48) "idle")
  ;; One setting: "start" (Start the run), on by default.
  (func (export "_start")
    (global.set $enabled (call $add_bool (i32.const 0) (i32.const 5) (i32.const 16) (i32.const 13) (i32.const 1))))
  ;; Every tick: the custom variable "phase" is "idle"; "start" is read from the settings map (as
  ;; asr's Gui::update does); with it on, a stopped timer starts.
  (func (export "update") (local $map i64) (local $value i64)
    (call $set_var (i32.const 32) (i32.const 5) (i32.const 48) (i32.const 4))
    (local.set $map (call $map_load))
    (local.set $value (call $map_get (local.get $map) (i32.const 0) (i32.const 5)))
    (if (i32.eqz (i64.eqz (local.get $value)))
      (then
        (if (call $get_bool (local.get $value) (i32.const 64))
          (then (global.set $enabled (i32.load8_u (i32.const 64)))))
        (call $value_free (local.get $value))))
    (call $map_free (local.get $map))
    (if (global.get $enabled)
      (then (if (i32.eqz (call $state)) (then (call $start)))))))
