// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2026 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

// Bindings for jQuery Terminal 2.48
// (https://terminal.jcubic.pl, https://www.npmjs.com/package/jquery.terminal).
//
// The surface follows the TypeScript declarations published with the npm
// package (`js/jquery.terminal.d.ts`). jQuery Terminal is a jQuery plugin: the
// npm module default-exports a factory that installs `$.fn.terminal` on a
// jQuery instance and returns that instance. The binding imports the factory
// from npm (`ImportDefault "jquery.terminal"`) and calls it once, caching the
// resulting jQuery object on `globalThis`, so the plugin is installed on the
// very same jQuery instance that `WebSharper.JQuery` imports from the `jquery`
// npm package. There is no CDN `<script>`.
//
// Where the upstream surface is `any`/`unknown` (JSON payloads, RPC responses)
// the binding keeps `obj` and documents it; everything else is typed.

namespace WebSharper.JQueryTerminal

open WebSharper
open WebSharper.JavaScript
open WebSharper.InterfaceGenerator

module Definition =

    module Ty =
        let Str = T<string>
        let Bool = T<bool>
        let Int = T<int>
        let Float = T<float>
        let Unit = T<unit>
        let Element = T<Dom.Element>
        let Event = T<WebSharper.JQuery.Event>
        let Promise = T<WebSharper.JQuery.Promise>
        let JQuery = T<WebSharper.JQuery.JQuery>
        let RegExp = T<System.Text.RegularExpressions.Regex>
        let AbortSignal = T<Dom.AbortSignal>
        let Blob = T<Blob>

    // -----------------------------------------------------------------
    // Forward declarations. Every class is created here and extended below,
    // so the classes can reference each other freely.
    // -----------------------------------------------------------------

    let Terminal = Class "Terminal"
    let History = Class "TerminalHistory"
    let Cmd = Class "TerminalCmd"
    let InterpreterObject = Class "TerminalInterpreterObject"
    let KeymapObject = Class "TerminalKeymap"
    let OptionsObject = Class "TerminalParsedOptions"
    let Exception = Class "TerminalException"

    module Enum =
        let TypingAnimation =
            Pattern.EnumStrings "TypingAnimation" [
                "echo"
                "prompt"
                "enter"
                "command"
            ]


    // -----------------------------------------------------------------
    // .NET names are PascalCase; the jQuery Terminal API is snake_case for
    // multi-word members (`get_output`, `scroll_to_bottom`, ...).
    // -----------------------------------------------------------------

    let private withJsNames (members: list<CodeModel.IClassMember>) : list<CodeModel.IClassMember> =
        // WIG uses the member name as the JavaScript call (`$this.<name>()`) and
        // `SourceName` as the .NET member name. Keep PascalCase in .NET and emit
        // the snake_case jQuery Terminal API (`GetOutput` -> `get_output`).
        let jsName (netName: string) =
            match netName with
            | "IsEnabled" -> "isenabled" // upstream spelling
            | name ->
                let sb = System.Text.StringBuilder()
                name |> Seq.iteri (fun i c ->
                    if System.Char.IsUpper c && i > 0 then sb.Append('_') |> ignore
                    sb.Append(System.Char.ToLowerInvariant c) |> ignore)
                sb.ToString()
        let rename (e: CodeModel.Entity) =
            let netName = e.Name
            e.Name <- jsName netName
            e.SourceName <- Some netName
        members
        |> List.map (fun m ->
            match m with
            | :? CodeModel.Method as me -> rename me; m
            | :? CodeModel.Property as p -> rename p; m
            | _ -> m)

    // -----------------------------------------------------------------
    // Callback shapes
    // -----------------------------------------------------------------

    /// `(command, term) => ...` called with `this` bound to the terminal.
    /// The upstream return type is `any`: a string/array is echoed when
    /// `processArguments` is off, a promise may be returned.
    let InterpreterFunction =
        (Terminal -* (Ty.Str * Terminal)) ^-> T<obj>

    /// `(setPrompt: (string) => void) => ...`
    let PromptFunction =
        (Terminal -* (Ty.Str ^-> Ty.Unit)) ^-> Ty.Unit

    /// `(setGreeting: (value) => void) => ...`
    let GreetingsFunction =
        (Terminal -* (Ty.Str ^-> Ty.Unit)) ^-> Ty.Unit

    /// `(str, callback: (string[]) => void) => void`
    let CompletionFunction =
        (Terminal -* (Ty.Str * ((!| Ty.Str) ^-> Ty.Unit))) ^-> Ty.Unit

    /// `(command) => bool`
    let HistoryFilterFunction = Ty.Str ^-> Ty.Bool

    /// `(command) => string[]`
    let ProcessArgumentsFunction = Ty.Str ^-> !| Ty.Str

    /// `(event) => bool` (returning `false` lets the key through)
    let KeyEventHandler = (Terminal -* Ty.Event) ^-> Ty.Bool

    /// `(event, original) => bool`
    let KeymapFunction =
        (Terminal -* (Ty.Event * (Ty.Event ^-> T<obj>))) ^-> T<obj>

    /// `(event, delta, term) => bool`
    let MouseWheelCallback =
        (Terminal -* (T<Dom.MouseEvent> * Ty.Float * Terminal)) ^-> Ty.Bool

    /// `(term) => void`
    let EventCallback = (Terminal -* Terminal) ^-> Ty.Bool

    /// `(user, tokenOrPass) => bool`
    let LoginBeforeCallback = (Terminal -* (Ty.Str * Ty.Str)) ^-> Ty.Bool

    /// `(user, token) => void`
    let LoginAfterCallback = (Terminal -* (Ty.Str * Ty.Str)) ^-> Ty.Unit

    /// `(username, password, callback) => void`
    let LoginFunction =
        (Ty.Str * Ty.Str * (Ty.Str ^-> Ty.Unit)) ^-> Ty.Unit

    /// `(setLoginCallback) => ...` (upstream allows a thenable to be returned)
    let SetLoginCallback = Ty.Str ^-> Ty.Unit

    /// `(e, label) => void`. Upstream passes a JS `Error` or a
    /// `TerminalException`; JS errors are not modelled by core, hence `obj`.
    let ExceptionHandler = (Terminal -* (T<obj> * Ty.Str)) ^-> Ty.Unit

    /// `(result, term) => void` where `result` is a JSON-RPC JSON object.
    let ProcessRPCResponseFunction =
        (Terminal -* (T<obj> * Terminal)) ^-> Ty.Unit

    /// `(xhr, json, term) => void`
    let RequestResponseCallback =
        (Terminal -* (T<WebSharper.JQuery.JqXHR> * T<obj> * Terminal)) ^-> Ty.Unit

    /// `(xhr, status, error) => void`
    let AjaxErrorCallback =
        (Terminal -* (T<WebSharper.JQuery.JqXHR> * Ty.Str * Ty.Str)) ^-> Ty.Unit

    /// `(error) => void` where `error` is the raw JSON-RPC error object.
    let RPCErrorCallback = (Terminal -* T<obj>) ^-> Ty.Unit

    /// `(command, term) => void`
    let CommandCallback = (Terminal -* (Ty.Str * Terminal)) ^-> Ty.Unit

    /// `(position, displayPosition, term) => void`
    let PositionCallback = (Terminal -* (Ty.Int * Ty.Int * Terminal)) ^-> Ty.Unit

    /// `(value: EchoValue) => void`
    let EchoCallback = (Terminal -* T<obj>) ^-> Ty.Unit

    /// `(div, command, term) => void`
    let EchoCommandCallback =
        (Terminal -* (Ty.JQuery * Ty.Str * Terminal)) ^-> Ty.Unit

    /// `(str, matched: string[], echoCmd: () => void) => bool | void`
    let DoubleTabFunction =
        (Terminal -* (Ty.Str * (!| Ty.Str) * (Ty.Unit ^-> Ty.Unit))) ^-> Ty.Bool

    /// `(value: string) => string | Blob | Promise<string | Blob> | void`
    let PasteCallback =
        (Terminal -* Ty.Str) ^-> (Ty.Str + Ty.Blob + T<Promise<string>> + Ty.Unit)

    let RenderHandlerOptions =
        Pattern.Config "RenderHandlerOptions" {
            Required = []
            Optional = [
                "update", Ty.Bool
                "line", Ty.Int
            ]
        }

    /// `(line, opts, term) => string | Element | JQuery | false | void`
    let RenderHandler =
        (Terminal -* (T<obj> * RenderHandlerOptions * Terminal))
        ^-> (Ty.Str + Ty.Element + Ty.JQuery + Ty.Bool)

    /// `(setPrompt) => void` for `Cmd` prompts
    let CmdPromptFunction = (Cmd -* (Ty.Str ^-> Ty.Unit)) ^-> Ty.Unit

    /// `(command) => void` for `Cmd.commands`
    let CmdCommandsFunction = (Cmd -* Ty.Str) ^-> T<obj>

    /// `(event) => bool` for `Cmd` key handlers
    let CmdKeyEventHandler = (Cmd -* Ty.Event) ^-> Ty.Bool

    /// `(value: string) => ...` for `Cmd.OnPaste`
    let CmdPasteCallback =
        (Cmd -* Ty.Str) ^-> (Ty.Str + Ty.Blob + T<Promise<string>> + Ty.Unit)

    // -----------------------------------------------------------------
    // Aliases
    // -----------------------------------------------------------------

    /// Upstream `echoValueOrPromise`. The function case produces a string or an
    /// array of strings; DOM elements and jQuery objects are echoed as HTML.
    let EchoValue =
        Ty.Str
        + Ty.Float
        + (!| (Ty.Str + Ty.Float))
        + Ty.Element
        + Ty.JQuery
        + (Ty.Unit ^-> Ty.Str)

    /// Upstream `ExtendedPrompt`: a string or a callback receiving `setPrompt`.
    let ExtendedPrompt = Ty.Str + PromptFunction

    /// Upstream `greetingsArg`: a string, a callback, or `null`.
    let Greetings = Ty.Str + GreetingsFunction

    /// Upstream `Completion`.
    let Completion = (!| Ty.Str) + CompletionFunction + Ty.Bool

    /// Upstream `historyFilter`: `null | RegExp | (command) => bool`.
    let HistoryFilter = Ty.RegExp + HistoryFilterFunction

    /// Upstream `LoginArgument`.
    let Login = Ty.Str + Ty.Bool + LoginFunction

    /// Upstream `InterpreterArgument` / `Interpreter`.
    let Interpreter = Ty.Str + InterpreterFunction + InterpreterObject

    /// A parsed argument: a number, a regular expression or a string.
    let Argument = Ty.Float + Ty.RegExp + Ty.Str

    /// An echo element that can be an unmount/onClear/finalize callback.
    let EchoEventFunction = (Terminal -* Ty.JQuery) ^-> Ty.Unit

    // -----------------------------------------------------------------
    // Shape types (defined before the option configs that reference them)
    // -----------------------------------------------------------------

    let TerminalPadding =
        Pattern.Config "TerminalPadding" {
            Required = []
            Optional = [
                "left", Ty.Int
                "right", Ty.Int
                "top", Ty.Int
                "bottom", Ty.Int
            ]
        }

    let TerminalSize =
        Pattern.Config "TerminalSize" {
            Required = [ ("width", Ty.Int) ]
            Optional = [ ("height", Ty.Int) ]
        }

    let TerminalGeometry =
        Pattern.Config "TerminalGeometry" {
            Required = []
            Optional = [
                "padding", TerminalPadding.Type
                "terminal", TerminalSize.Type
                "char", TerminalSize.Type
                "cols", Ty.Int
                "rows", Ty.Int
            ]
        }

    let LineEchoOptions =
        Pattern.Config "LineEchoOptions" {
            Required = []
            Optional = [
                "exec", Ty.Bool
                "unmount", EchoEventFunction
                "onClear", EchoEventFunction
                "finalize", EchoEventFunction
                "invokeMethods", Ty.Bool
                "allowedAttributes", !| (Ty.RegExp + Ty.Str)
                "delay", Ty.Float
                "ansi", Ty.Bool
                "typing", Ty.Bool
                "flush", Ty.Bool
                "formatters", Ty.Bool
                "keepWords", Ty.Bool
                "raw", Ty.Bool
                "newline", Ty.Bool
            ]
        }

    let InterpreterItem =
        Pattern.Config "InterpreterItem" {
            Required = []
            Optional = [
                "completion", Completion
                "history", Ty.Bool
                "interpreter", InterpreterFunction
                "keydown", KeyEventHandler
                "keypress", KeyEventHandler
                "mask", Ty.Bool + Ty.Str
                "infiniteLogin", Ty.Bool
                "prompt", ExtendedPrompt
            ]
        }

    let TerminalLine =
        Pattern.Config "TerminalLine" {
            Required = []
            Optional = [
                "string", Ty.Str
                "options", LineEchoOptions.Type
                "index", Ty.Int
            ]
        }

    let TerminalView =
        Pattern.Config "TerminalView" {
            Required = []
            Optional = [
                "focus", Ty.Bool
                "mask", Ty.Bool + Ty.Str
                "prompt", ExtendedPrompt
                "command", Ty.Str
                "position", Ty.Int
                "lines", !| TerminalLine
                "history", !| Ty.Str
            ]
        }

    let EchoOptions =
        Pattern.Config "EchoOptions" {
            Required = []
            Optional = [
                "flush", Ty.Bool
                "raw", Ty.Bool
                "exec", Ty.Bool
                "invokeMethods", Ty.Bool
                "ansi", Ty.Bool
                "allowedAttributes", !| (Ty.RegExp + Ty.Str)
                "unmount", EchoEventFunction
                "onClear", EchoEventFunction
                "finalize", EchoEventFunction
                "keepWords", Ty.Bool
                "formatters", Ty.Bool
                "newline", Ty.Bool
                // animation options accepted by the promise-returning overloads
                "delay", Ty.Float
                "typing", Ty.Bool
            ]
        }

    let AnimationOptions =
        Pattern.Config "AnimationOptions" {
            Required = []
            Optional = [
                "delay", Ty.Float
                "typing", Ty.Bool
            ]
        }

    let InsertOptions =
        Pattern.Config "InsertOptions" {
            Required = []
            Optional = [
                "stay", Ty.Bool
                "delay", Ty.Float
                "typing", Ty.Bool
            ]
        }

    let ReadOptions =
        Pattern.Config "ReadOptions" {
            Required = []
            Optional = [
                "typing", Ty.Bool
                "delay", Ty.Float
                "success", (Ty.Str ^-> Ty.Unit)
                "cancel", (Ty.Unit ^-> Ty.Unit)
            ]
        }

    let CompleteOptions =
        Pattern.Config "CompleteOptions" {
            Required = []
            Optional = [
                "word", Ty.Bool
                "echo", Ty.Bool
                "escape", Ty.Bool
                "caseSensitive", Ty.Bool
                "echoCommand", Ty.Bool
                "doubleTab", DoubleTabFunction
            ]
        }

    let ExecOptions =
        Pattern.Config "ExecOptions" {
            Required = []
            Optional = [
                "delay", Ty.Float
                "typing", Ty.Bool
                "silent", Ty.Bool
                "history", Ty.Bool
            ]
        }

    let FormatOptions =
        Pattern.Config "FormatOptions" {
            Required = []
            Optional = [
                "linksNoReferrer", Ty.Bool
                "anyLinks", Ty.Bool
                "charWidth", Ty.Int
                "linksNoFollow", Ty.Bool
                "allowedAttributes", !| Ty.Str
                "escape", Ty.Bool
            ]
        }

    let IterateFormattingArgument =
        Pattern.Config "IterateFormattingArgument" {
            Required = []
            Optional = [
                "count", Ty.Int
                "index", Ty.Int
                "formatting", Ty.Str
                "length", Ty.Int
                "text", Ty.Bool
                "space", Ty.Int
            ]
        }

    let TerminalStrings =
        Pattern.Config "TerminalStrings" {
            Required = []
            Optional = [
                "comletionParameters", Ty.Str
                "wrongPasswordTryAgain", Ty.Str
                "wrongPassword", Ty.Str
                "ajaxAbortError", Ty.Str
                "wrongArity", Ty.Str
                "commandNotFound", Ty.Str
                "oneRPCWithIgnore", Ty.Str
                "oneInterpreterFunction", Ty.Str
                "loginFunctionMissing", Ty.Str
                "noTokenError", Ty.Str
                "serverResponse", Ty.Str
                "wrongGreetings", Ty.Str
                "notWhileLogin", Ty.Str
                "loginIsNotAFunction", Ty.Str
                "canExitError", Ty.Str
                "invalidCompletion", Ty.Str
                "invalidCompletionFunction", Ty.Str
                "invalidSelector", Ty.Str
                "invalidTerminalId", Ty.Str
                "login", Ty.Str
                "password", Ty.Str
                "recursiveCall", Ty.Str
                "notAString", Ty.Str
                "redrawError", Ty.Str
                "invalidStrings", Ty.Str
                "defunctTerminal", Ty.Str
            ]
        }

    let ParsedCommand =
        Pattern.Config "ParsedCommand" {
            Required = []
            Optional = [
                "command", Ty.Str
                "name", Ty.Str
                "args", !| Argument
                "args_quotes", !| Ty.Str
                "rest", Ty.Str
            ]
        }

    let ParsedCommandString =
        Pattern.Config "ParsedCommandString" {
            Required = []
            Optional = [
                "command", Ty.Str
                "name", Ty.Str
                "args", !| Ty.Str
                "args_quotes", !| Ty.Str
                "rest", Ty.Str
            ]
        }

    let PushOptions =
        Pattern.Config "PushOptions" {
            Required = []
            Optional = [
                "completion", Completion
                "keypress", KeyEventHandler
                "keydown", KeyEventHandler
                "mousewheel", MouseWheelCallback
                "touchscroll", MouseWheelCallback
                "keymap", KeymapObject.Type
                "history", Ty.Bool
                "name", Ty.Str
                "prompt", ExtendedPrompt
                "login", Login
                "onFocus", EventCallback
                "onClear", EventCallback
                "onBlur", EventCallback
                "onExit", EventCallback
                "onPop", (Terminal -* (InterpreterItem * InterpreterItem * Terminal)) ^-> Ty.Unit
                "onPush", (Terminal -* (InterpreterItem * InterpreterItem * Terminal)) ^-> Ty.Unit
                "onTerminalChange", EventCallback
                "onAfterRedraw", EventCallback
                "onEchoCommand", EchoCommandCallback
                "onFlush", EventCallback
                "onPaste", PasteCallback
                "onCommandChange", CommandCallback
                "onPositionChange", PositionCallback
                "onBeforeCommand", (Terminal -* Ty.Str) ^-> Ty.Bool
                "onAfterCommand", (Terminal -* Ty.Str) ^-> Ty.Unit
                "onBeforeEcho", (Terminal -* EchoValue) ^-> Ty.Bool
                "onAfterEcho", (Terminal -* EchoValue) ^-> Ty.Unit
                "infiniteLogin", Ty.Bool
                "onStart", (Ty.Unit ^-> Ty.Unit)
            ]
        }

    let TerminalOptions =
        Pattern.Config "TerminalOptions" {
            Required = []
            Optional = [
                "completion", Completion
                "keypress", KeyEventHandler
                "keydown", KeyEventHandler
                "mousewheel", MouseWheelCallback
                "touchscroll", MouseWheelCallback
                "keymap", KeymapObject.Type
                "history", Ty.Bool
                "name", Ty.Str
                "prompt", ExtendedPrompt
                "login", Login
                "onFocus", EventCallback
                "onClear", EventCallback
                "onBlur", EventCallback
                "onExit", EventCallback
                "onPop", (Terminal -* (InterpreterItem * InterpreterItem * Terminal)) ^-> Ty.Unit
                "onPush", (Terminal -* (InterpreterItem * InterpreterItem * Terminal)) ^-> Ty.Unit
                "onTerminalChange", EventCallback
                "onAfterRedraw", EventCallback
                "onEchoCommand", EchoCommandCallback
                "onFlush", EventCallback
                "onPaste", PasteCallback
                "onCommandChange", CommandCallback
                "onPositionChange", PositionCallback
                "onBeforeCommand", (Terminal -* Ty.Str) ^-> Ty.Bool
                "onAfterCommand", (Terminal -* Ty.Str) ^-> Ty.Unit
                "onBeforeEcho", (Terminal -* EchoValue) ^-> Ty.Bool
                "onAfterEcho", (Terminal -* EchoValue) ^-> Ty.Unit
                "onBeforeLogout", (Terminal -* Terminal) ^-> Ty.Bool
                "onAfterLogout", EventCallback
                "onBeforeLogin", LoginBeforeCallback
                "onAfterLogin", LoginAfterCallback
                "execHash", Ty.Bool
                "execHistory", Ty.Bool
                "exit", Ty.Bool
                "clear", Ty.Bool
                "enabled", Ty.Bool
                "maskChar", Ty.Str
                "wrap", Ty.Bool
                "checkArity", Ty.Bool
                "invokeMethods", Ty.Bool
                "useCache", Ty.Bool
                "cacheSize", Ty.Int
                "anyLinks", Ty.Bool
                "id", Ty.Int
                "raw", Ty.Bool
                "allowedAttributes", !| (Ty.RegExp + Ty.Str)
                "tabindex", Ty.Int
                "exceptionHandler", ExceptionHandler
                "pauseEvents", Ty.Bool
                "softPause", Ty.Bool
                "memory", Ty.Bool
                "cancelableAjax", Ty.Bool
                "processArguments", Ty.Bool + ProcessArgumentsFunction
                "execAnimation", Ty.Bool
                "linksNoReferrer", Ty.Bool
                "javascriptLinks", Ty.Bool
                "processRPCResponse", ProcessRPCResponseFunction
                "completionEscape", Ty.Bool
                "convertLinks", Ty.Bool
                "errorOnAbort", Ty.Bool
                "extra", T<obj> // arbitrary application payload, upstream `any`
                "tabs", Ty.Int
                "historySize", Ty.Int
                "greetings", Greetings
                "scrollObject", Ty.Str + Ty.Element + Ty.JQuery
                "historyState", Ty.Bool
                "importHistory", Ty.Bool
                "historyFilter", HistoryFilter
                "echoCommand", Ty.Bool
                "scrollOnEcho", Ty.Bool
                "outputLimit", Ty.Int
                "pasteImage", Ty.Bool
                "scrollBottomOffset", Ty.Bool
                "wordAutocomplete", Ty.Bool
                "caseSensitiveAutocomplete", Ty.Bool
                "caseSensitiveSearch", Ty.Bool
                "clickTimeout", Ty.Int
                "holdTimeout", Ty.Int
                "holdRepeatTimeout", Ty.Int
                "repeatTimeoutKeys", !| Ty.Str
                "mobileIgnoreAutoSpace", !| Ty.Str
                "request", RequestResponseCallback
                "response", RequestResponseCallback
                "describe", Ty.Str + Ty.Bool
                "onRPCError", RPCErrorCallback
                "doubleTab", DoubleTabFunction
                "doubleTabEchoCommand", Ty.Bool
                "renderHandler", RenderHandler
                "onAjaxError", AjaxErrorCallback
                "onInit", EventCallback
                "autocompleteMenu", Ty.Bool
                "mobileDelete", Ty.Bool
                "strings", TerminalStrings.Type
                "height", Ty.Int
            ]
        }

    // -----------------------------------------------------------------
    // Classes
    // -----------------------------------------------------------------

    // The plugin factory. Calling it with `window` installs `$.fn.terminal` on
    // the bundled jQuery instance and returns that jQuery object. The result is
    // cached on the global object, so every constructor and helper below works
    // with one shared jQuery instance and installs the plugin only once.
    let jqTerminal =
        "(globalThis.__wsJQueryTerminal || (globalThis.__wsJQueryTerminal = $import(window)))"

    Terminal
    |+> Static [
        "Load" => Ty.Unit ^-> Ty.Unit
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ";")
        |> WithComment
            "Installs the jQuery Terminal plugin on the bundled jQuery instance. \
             Called automatically by the constructors; call it explicitly before \
             using the static helpers below if you do not create a terminal first."

        Constructor(Ty.Str?``target``)
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + "($target).terminal()")
        |> WithComment "Creates a terminal on the given selector."

        Constructor(Ty.Str?``target`` * Interpreter?interpreter)
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + "($target).terminal($interpreter)")
        |> WithComment "Creates a terminal with an interpreter."

        Constructor(Ty.Str?``target`` * Interpreter?interpreter * TerminalOptions?options)
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + "($target).terminal($interpreter, $options)")
        |> WithComment "Creates a terminal with an interpreter and options."

        Constructor(Ty.Element?``target`` * Interpreter?interpreter * TerminalOptions?options)
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + "($target).terminal($interpreter, $options)")
        |> WithComment "Creates a terminal on a DOM element."

        "PluginVersion" =? Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithGetterInline (jqTerminal + ".terminal.version")
        |> WithComment "The jQuery Terminal version (`$.terminal.version`)."

        "Format" => Ty.Str?``string`` * !? FormatOptions?options ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.format($string, $options)")
        |> WithComment "Formats command output (ANSI, links, formatting)."

        "EscapeBrackets" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.escape_brackets($string)")

        "UnescapeBrackets" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.unescape_brackets($string)")

        "EscapeFormatting" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.escape_formatting($string)")

        "HaveFormatting" => Ty.Str?``string`` ^-> Ty.Bool
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.have_formatting($string)")

        "IsFormatting" => Ty.Str?``string`` ^-> Ty.Bool
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.is_formatting($string)")

        "FormatSplit" => Ty.Str?``string`` ^-> !| Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.format_split($string)")

        "Strip" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.strip($string)")

        "Length" => Ty.Str?``string`` ^-> Ty.Int
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.length($string)")

        "Normalize" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.normalize($string)")

        "SplitEqual" => Ty.Str?``string`` * Ty.Int?``length`` * !? Ty.Bool?``keepWords`` ^-> !| Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.split_equal($string, $length, $keepWords)")

        "Columns" => (!| Ty.Str)?``array`` * Ty.Int?``cols`` * Ty.Int?``space`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.columns($array, $cols, $space)")

        "Amp" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.amp($string)")

        "Encode" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.encode($string)")

        "ValidColor" => Ty.Str?``color`` ^-> Ty.Bool
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.valid_color($color)")

        "UnclosedStrings" => Ty.Str?``string`` ^-> Ty.Bool
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.unclosed_strings($string)")

        "EscapeRegex" => Ty.Str?``string`` ^-> Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.escape_regex($string)")

        "ParseArgument" => Ty.Str?``argument`` * !? Ty.Bool?``strict`` ^-> Argument
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.parse_argument($argument, $strict)")

        "ParseArguments" => Ty.Str?``string`` ^-> !| Argument
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.parse_arguments($string)")

        "SplitArguments" => Ty.Str?``string`` ^-> !| Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.split_arguments($string)")

        "ParseCommand" => Ty.Str?``string`` ^-> ParsedCommand
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.parse_command($string)")

        "SplitCommand" => Ty.Str?``string`` ^-> ParsedCommandString
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.split_command($string)")

        "ParseOptions" => (Ty.Str + !| Ty.Str)?``argument`` ^-> OptionsObject
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.parse_options($argument)")

        "ParseFormatting" => Ty.Str?``string`` ^-> !| Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.parse_formatting($string)")

        "IterateFormatting" =>
            Ty.Str?``string`` * (IterateFormattingArgument ^-> Ty.Unit)?``callback`` ^-> Ty.Unit
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.iterate_formatting($string, $callback)")

        "Active" => Ty.Unit ^-> !? Terminal
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.active()")
        |> WithComment "The currently active terminal, if any."

        "LastId" => Ty.Unit ^-> Ty.Int
        |> ImportDefault "jquery.terminal"
        |> WithInline (jqTerminal + ".terminal.last_id()")

        "CreateHistory" => !? Ty.Str?``name`` * !? Ty.Int?``size`` * !? Ty.Bool?``memory`` ^-> History
        |> ImportDefault "jquery.terminal"
        |> WithInline ("new " + jqTerminal + ".terminal.History($name, $size, $memory)")
        |> WithComment "Creates a standalone command history."

        "ColorNames" =? !| Ty.Str
        |> ImportDefault "jquery.terminal"
        |> WithGetterInline (jqTerminal + ".terminal.color_names")
    ]
    |> ignore

    Cmd
    |+> Instance (withJsNames [
        "Option" => Ty.Str?``name`` ^-> (Ty.Str + Ty.Bool + Ty.Float)
        "Option" => Ty.Str?``name`` * (Ty.Str + Ty.Bool + Ty.Float)?``value`` ^-> TSelf
        "Name" => Ty.Unit ^-> Ty.Str
        "Name" => Ty.Str?``name`` ^-> TSelf
        "Purge" => Ty.Unit ^-> TSelf
        "History" => Ty.Unit ^-> History
        "Delete" => Ty.Int?``count`` * !? Ty.Bool?``stay`` ^-> Ty.Str
        "Set" => Ty.Str?``command`` * !? Ty.Bool?``stay`` * !? Ty.Bool?``silent`` ^-> TSelf
        "Keymap" => Ty.Str?``shortcut`` * KeymapFunction?``callback`` ^-> TSelf
        "Keymap" => Ty.Str?``shortcut`` ^-> KeymapFunction
        "Keymap" => KeymapObject?``keymap`` ^-> TSelf
        "Insert" => Ty.Str?``value`` * !? Ty.Bool?``stay`` ^-> TSelf
        "Commands" => CmdCommandsFunction?``fn`` ^-> TSelf
        "Commands" => Ty.Unit ^-> CmdCommandsFunction
        "Destroy" => Ty.Unit ^-> TSelf
        "InvokeKey" => Ty.Str?``shortcut`` ^-> TSelf
        "Column" => Ty.Bool?``includePrompt`` ^-> Ty.Int
        "Prompt" => (Ty.Str + CmdPromptFunction)?``prompt`` ^-> TSelf
        "Prompt" => Ty.Unit ^-> ExtendedPrompt
        "KillText" => Ty.Unit ^-> Ty.Str
        "Position" => Ty.Unit ^-> Ty.Int
        "Position" => Ty.Int?``value`` * !? Ty.Bool?``silent`` ^-> TSelf
        "Refresh" => Ty.Unit ^-> TSelf
        "DisplayPosition" => Ty.Unit ^-> Ty.Int
        "DisplayPosition" => Ty.Int?``value`` ^-> TSelf
        "Visible" => Ty.Unit ^-> TSelf
        "ClearCache" => Ty.Unit ^-> TSelf
        "Enable" => Ty.Unit ^-> TSelf
        "IsEnabled" => Ty.Unit ^-> Ty.Bool
        "Disable" => !? Ty.Bool?``focus`` ^-> TSelf
        "Mask" => (Ty.Bool + Ty.Str)?``mask`` ^-> TSelf
    ])
    |> ignore

    History
    |+> Instance (withJsNames [
        "Append" => Ty.Str?``item`` ^-> Ty.Unit
        "Set" => (!| Ty.Str)?``items`` ^-> Ty.Unit
        "Data" => Ty.Unit ^-> !| Ty.Str
        "Reset" => Ty.Unit ^-> Ty.Unit
        "Last" => Ty.Unit ^-> !? Ty.Str
        "End" => Ty.Unit ^-> Ty.Bool
        "Position" => Ty.Unit ^-> Ty.Int
        "Current" => Ty.Unit ^-> Ty.Str
        "Next" => Ty.Unit ^-> !? Ty.Str
        "Previous" => Ty.Unit ^-> !? Ty.Str
        "Clear" => Ty.Unit ^-> Ty.Unit
        "Enabled" => Ty.Unit ^-> Ty.Bool
        "Enable" => Ty.Unit ^-> Ty.Unit
        "Purge" => Ty.Unit ^-> Ty.Unit
        "Disable" => Ty.Unit ^-> Ty.Unit
        "Toggle" => !? Ty.Bool?``value`` ^-> Ty.Unit
    ])
    |> ignore

    // Keymap objects are plain maps of shortcut -> handler.
    KeymapObject
    |+> Instance [
        Property "" KeymapFunction |> Indexed Ty.Str
    ]
    |> ignore

    // Interpreters can nest: a command name maps to a string, a callback or a
    // nested interpreter object.
    InterpreterObject
    |+> Instance [
        Property "" (Ty.Str + InterpreterFunction + TSelf) |> Indexed Ty.Str
    ]
    |> ignore

    OptionsObject
    |+> Instance [
        "_" =? !| Ty.Str
        |> WithSourceName "Arguments"
        Property "" (Ty.Bool + Ty.Str + (!| Ty.Str)) |> Indexed Ty.Str
    ]
    |> ignore

    Terminal
    |+> Instance (withJsNames [
        "AsJQuery" => Ty.Unit ^-> Ty.JQuery
        |> WithInline "$this"
        |> WithComment "The terminal as its underlying jQuery object."

        "Id" => Ty.Unit ^-> Ty.Int
        "Clear" => Ty.Unit ^-> TSelf
        "Ready" => Ty.Unit ^-> Ty.Promise
        "OutputReady" => Ty.Unit ^-> Ty.Promise
        "ExportView" => Ty.Unit ^-> TerminalView
        "ImportView" => TerminalView?``view`` ^-> TSelf
        "SaveState" => !? Ty.Str?``command`` * !? Ty.Bool?``ignoreHash`` * !? Ty.Int?``index`` ^-> TSelf

        "Exec" => Ty.Str?``command`` ^-> Ty.Promise
        "Exec" => Ty.Str?``command`` * Ty.Bool?``silent`` ^-> Ty.Promise
        "Exec" => Ty.Str?``command`` * ExecOptions?``options`` ^-> Ty.Promise
        "Exec" => Ty.Str?``command`` * Ty.Bool?``silent`` * ExecOptions?``options`` ^-> Ty.Promise

        "Autologin" => Ty.Str?``user`` * Ty.Str?``token`` * !? Ty.Bool?``silent`` ^-> TSelf
        "Login" =>
            LoginFunction?``auth``
            * !? Ty.Bool?``infinite``
            * !? (Ty.Unit ^-> Ty.Unit)?``success``
            * !? (Ty.Unit ^-> Ty.Unit)?``error``
            ^-> TSelf
        "Logout" => !? Ty.Bool?``local`` ^-> TSelf

        "Settings" => Ty.Unit ^-> TerminalOptions
        |> WithComment "The current settings. Missing fields fall back to the defaults."

        "BeforeCursor" => !? Ty.Bool?``word`` ^-> Ty.Str
        "Complete" => (!| Ty.Str)?``commands`` * !? CompleteOptions?``options`` ^-> Ty.Bool
        "Commands" => Ty.Unit ^-> InterpreterFunction
        "SetInterpreter" => Interpreter?``interpreter`` * !? Login?``login`` ^-> TSelf
        "Greetings" => Ty.Unit ^-> TSelf

        "Paused" => Ty.Unit ^-> Ty.Bool
        "Pause" => Ty.Unit ^-> TSelf
        "Resume" => Ty.Unit ^-> TSelf
        "Signal" => Ty.Unit ^-> Ty.AbortSignal
        "Abort" => !? Ty.Str?``message`` ^-> TSelf

        "Cols" => Ty.Unit ^-> Ty.Int
        "Rows" => Ty.Unit ^-> Ty.Int
        "Geometry" => Ty.Unit ^-> TerminalGeometry

        "History" => Ty.Unit ^-> History
        "HistoryState" => Ty.Bool?``toggle`` ^-> TSelf
        "ClearHistoryState" => Ty.Unit ^-> TSelf

        "Focus" => !? Ty.Bool?``toggle`` * !? Ty.Bool?``silent`` ^-> TSelf
        "Blur" => !? Ty.Bool?``silent`` ^-> TSelf
        "Freeze" => !? Ty.Bool?``toggle`` ^-> TSelf
        "Frozen" => Ty.Unit ^-> Ty.Bool
        "Enable" => !? Ty.Bool?``silent`` ^-> TSelf
        "Disable" => !? Ty.Bool?``silent`` ^-> TSelf
        "Enabled" => Ty.Unit ^-> Ty.Bool
        "Signature" => Ty.Unit ^-> Ty.Str
        "Version" => Ty.Unit ^-> Ty.Str
        "Cmd" => Ty.Unit ^-> Cmd

        "GetCommand" => Ty.Unit ^-> Ty.Str
        "SetCommand" => Ty.Str?``command`` * !? Ty.Bool?``silent`` ^-> TSelf
        "SetPosition" => Ty.Int?``position`` * !? Ty.Bool?``relative`` ^-> TSelf
        "GetPosition" => Ty.Unit ^-> Ty.Int

        "Enter" => Ty.Str?``value`` ^-> TSelf
        "Enter" => Ty.Str?``value`` * AnimationOptions?``options`` ^-> Ty.Promise
        "Insert" => Ty.Str?``value`` * !? Ty.Bool?``stay`` ^-> TSelf
        "Insert" => Ty.Str?``value`` * InsertOptions?``options`` ^-> Ty.Promise

        "SetPrompt" => ExtendedPrompt?``prompt`` ^-> TSelf
        "SetPrompt" => ExtendedPrompt?``prompt`` * AnimationOptions?``options`` ^-> Ty.Promise
        "GetPrompt" => Ty.Unit ^-> ExtendedPrompt

        "SetMask" => !? (Ty.Bool + Ty.Str)?``toggle`` ^-> TSelf
        "GetOutput" => Ty.Unit ^-> Ty.Str
        |> WithComment "The whole terminal output as plain text."
        "GetOutput" => Ty.Bool?``raw`` ^-> !| TerminalLine
        |> WithComment "With `raw = true`, the output lines with their rendering options."
        "Resize" => !? Ty.Int?``width`` * !? Ty.Int?``height`` ^-> TSelf
        "Refresh" => Ty.Unit ^-> TSelf
        "ClearCache" => Ty.Unit ^-> TSelf
        "Flush" => Ty.Unit ^-> TSelf
        "Update" => Ty.Int?``line`` * Ty.Str?``string`` * !? EchoOptions?``options`` ^-> TSelf
        "RemoveLine" => Ty.Int?``line`` ^-> TSelf
        "LastIndex" => Ty.Unit ^-> Ty.Int

        "Echo" => EchoValue?``value`` * !? EchoOptions?``options`` ^-> TSelf
        |> WithComment
            "Writes a value to the terminal. Elements and jQuery objects are echoed as HTML. \
             Animated echoes return a promise upstream; this overload returns the terminal."
        "EchoHtml" => Ty.Str?``html`` ^-> TSelf
        |> WithInline "$this.echo($html, { raw: true })"
        |> WithComment "Convenience for `Echo(value, EchoOptions(Raw = true))`."
        "Error" => (Ty.Str + (Ty.Unit ^-> Ty.Str))?``message`` * !? EchoOptions?``options`` ^-> TSelf
        "Exception" => T<obj>?``error`` * !? Ty.Str?``label`` ^-> TSelf
        |> WithComment "Displays a JS `Error` (not modelled by core) or a `TerminalException`."
        "Scroll" => Ty.Int?``amount`` ^-> TSelf
        "ScrollToBottom" => Ty.Unit ^-> TSelf
        "IsBottom" => Ty.Unit ^-> Ty.Bool

        "Token" => !? Ty.Bool?``local`` ^-> !? Ty.Str
        "SetToken" => !? Ty.Str?``token`` * !? Ty.Bool?``local`` ^-> TSelf
        "GetToken" => !? Ty.Bool?``local`` ^-> !? Ty.Str
        "LoginName" => !? Ty.Bool?``local`` ^-> !? Ty.Str
        "Name" => Ty.Unit ^-> Ty.Str
        "PrefixName" => !? Ty.Bool?``local`` ^-> Ty.Str

        "Typing" =>
            Enum.TypingAnimation?``typing``
            * Ty.Float?``delay``
            * Ty.Str?``message``
            * (Ty.Unit ^-> Ty.Unit)?``finish``
            ^-> Ty.Promise
        "Skip" => Ty.Unit ^-> TSelf
        "SkipStop" => Ty.Unit ^-> TSelf

        "Read" =>
            Ty.Str?``message``
            * !? ((Ty.Str ^-> Ty.Unit) + ReadOptions)?``successOrOptions``
            * !? (Ty.Unit ^-> Ty.Unit)?``cancel``
            ^-> T<Promise<string>>

        "Push" => Interpreter?``interpreter`` * !? PushOptions?``options`` ^-> TSelf
        "Pop" => !? Ty.Str?``echoCommand`` * !? Ty.Bool?``silent`` ^-> TSelf
        "Level" => Ty.Unit ^-> Ty.Int

        "Option" => Ty.Str?``name`` ^-> (Ty.Str + Ty.Bool + Ty.Float)
        "Option" => TerminalOptions?``options`` ^-> T<obj>
        |> WithComment "Applies options; upstream returns `any` here, so the result is untyped."
        "Option" => Ty.Str?``name`` * (Ty.Str + Ty.Bool + Ty.Float)?``value`` ^-> TSelf

        "InvokeKey" => Ty.Str?``shortcut`` ^-> TSelf
        "Keymap" => Ty.Str?``shortcut`` * KeymapFunction?``callback`` ^-> TSelf
        "Keymap" => Ty.Str?``shortcut`` ^-> KeymapFunction
        "Keymap" => KeymapObject?``keymap`` ^-> TSelf

        "Reset" => Ty.Unit ^-> TSelf
        "Purge" => Ty.Unit ^-> TSelf
        "Destroy" => Ty.Unit ^-> TSelf
    ])
    |> ignore

    Exception
    |+> Instance (withJsNames [
        "Message" =? Ty.Str
        "Type" =? Ty.Str
        "Stack" =? Ty.Str
    ])
    |> ignore

    let Assembly =
        Assembly [
            Namespace "WebSharper.JQueryTerminal" [
                Terminal
                History
                Cmd
                InterpreterObject
                KeymapObject
                OptionsObject
                TerminalOptions
                PushOptions
                EchoOptions
                AnimationOptions
                InsertOptions
                ReadOptions
                CompleteOptions
                ExecOptions
                FormatOptions
                IterateFormattingArgument
                TerminalStrings
                TerminalSize
                TerminalGeometry
                TerminalPadding
                RenderHandlerOptions
                TerminalLine
                LineEchoOptions
                TerminalView
                InterpreterItem
                ParsedCommand
                ParsedCommandString
                Enum.TypingAnimation
                Exception
            ]
        ]

[<Sealed>]
type Extension() =
    interface IExtension with
        member x.Assembly = Definition.Assembly

[<assembly: Extension(typeof<Extension>)>]
do ()