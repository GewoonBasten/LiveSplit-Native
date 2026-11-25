;; An auto splitter with every kind of settings widget, for proof 104 (the Auto Splitting Runtime's
;; settings page). Hand-assembled into asr-widgets.wasm by tools/wat/asr_test_wasm.py, which lays
;; the strings out one after another from address 0 (shown here by name).
(module
  (import "env" "user_settings_add_title" (func $add_title (param i32 i32 i32 i32 i32)))
  (import "env" "user_settings_add_bool" (func $add_bool (param i32 i32 i32 i32 i32) (result i32)))
  (import "env" "user_settings_add_choice" (func $add_choice (param i32 i32 i32 i32 i32 i32)))
  (import "env" "user_settings_add_choice_option" (func $add_option (param i32 i32 i32 i32 i32 i32) (result i32)))
  (import "env" "user_settings_add_file_select" (func $add_file (param i32 i32 i32 i32)))
  (import "env" "user_settings_add_file_select_name_filter" (func $add_filter (param i32 i32 i32 i32 i32 i32)))
  (import "env" "user_settings_set_tooltip" (func $tooltip (param i32 i32 i32 i32)))
  (memory (export "memory") 1)
  (func (export "_start")
    ;; title "general" "General" level 0
    ;; bool "start" "Start the run" (on)
    ;; bool "levels" "Split on levels" (off), tooltip "Splits when a level is finished"
    ;; title "advanced" "Advanced" level 1
    ;; choice "version" "Game version" default "jp": "jp" Japanese, "us" North American
    ;; file select "save" "Select save file", filter "Save files" *.sav
    )
  (func (export "update")))
