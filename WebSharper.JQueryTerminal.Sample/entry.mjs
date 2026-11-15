// jQuery Terminal is a jQuery plugin distributed as a UMD/CommonJS module. The
// WebSharper binding imports its factory from the npm package and installs it on
// the bundled jQuery instance, so no side-effect import is needed here. We do
// pull in the package's stylesheet, and then the WebSharper-generated bundle.
import "jquery.terminal/css/jquery.terminal.min.css"

import "./build/WebSharper.JQueryTerminal.Sample.js"