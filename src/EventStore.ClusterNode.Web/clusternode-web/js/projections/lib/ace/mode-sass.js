/* ***** BEGIN LICENSE BLOCK *****
 * Distributed under the BSD license:
 *
 * Copyright (c) 2012, Ajax.org B.V.
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of Ajax.org B.V. nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL AJAX.ORG B.V. BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 *
 * Contributor(s):
 * 
 *
 *
 * ***** END LICENSE BLOCK ***** */

define('ace/mode/sass', ['require', 'exports', 'module' , 'ace/lib/oop', 'ace/mode/text', 'ace/tokenizer', 'ace/mode/sass_highlight_rules'], function(require, exports, module) {


var oop = require("../lib/oop");
var TextMode = require("./text").Mode;
var Tokenizer = require("../tokenizer").Tokenizer;
var SASSHighlightRules = require("./sass_highlight_rules").SASSHighlightRules;

var Mode = function() {
    var highlighter = new SASSHighlightRules();
    
    this.$tokenizer = new Tokenizer(highlighter.getRules());
};
oop.inherits(Mode, TextMode);

(function() {
}).call(Mode.prototype);

exports.Mode = Mode;
});

define('ace/mode/sass_highlight_rules', ['require', 'exports', 'module' , 'ace/lib/oop', 'ace/mode/text_highlight_rules'], function(require, exports, module) {


var oop = require("../lib/oop");
var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;

var SASSHighlightRules = function() {

    this.$rules = 
        {
    "start": [
        {
            "token": [
                "keyword.control.untitled",
                "keyword.control.untitled"
            ],
            "regex": "\\b(a|abbr|acronym|address|area|article|aside|audio|b|base|big|blockquote|body|br|button|canvas|caption|cite|code|col|colgroup|command|datalist|dd|del|details|dfn|div|dl|dt|em|embed|fieldset|figcaption|figure|footer|form|frame|frameset|(h[1-6])|head|header|hgroup|hr|html|i|iframe|img|input|ins|kbd|keygen|label|legend|li|link|map|mark|meta|meter|nav|noframes|noscript|object|ol|optgroup|option|output|p|param|pre|progress|q|rp|rt|ruby|samp|script|section|select|small|span|strike|strong|style|sub|sup|table|tbody|td|textarea|tfoot|th|thead|time|title|tr|tt|ul|var|video)\\b"
        },
        {
            "token": [
                "keyword.control.at-rule.sass",
                "keyword.control.at-rule.sass"
            ],
            "regex": "@(import|mixin|include|charset|import|media|page|font-face|namespace) [\\/\\.\\w-]*\\b"
        },
        {
            "token": [
                null,
                "string.quoted.double.sass"
            ],
            "regex": "\"",
            "next": "state_3"
        },
        {
            "token": [
                "entity.other.attribute-name.class.sass",
                "entity.other.attribute-name.class.sass"
            ],
            "regex": "\\.[a-zA-Z0-9_-]+"
        },
        {
            "token": [
                "constant.other.rgb-value.sass",
                "constant.other.rgb-value.sass"
            ],
            "regex": "(#)([0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\b"
        },
        {
            "token": [
                "entity.other.attribute-name.id.sass",
                "entity.other.attribute-name.id.sass"
            ],
            "regex": "#[a-zA-Z0-9_-]+"
        },
        {
            "token": [
                "variable.parameter.sass",
                "variable.parameter.sass"
            ],
            "regex": "[!\\$][a-zA-Z0-9_-]+"
        },
        {
            "token": [
                null,
                "comment.block.sass"
            ],
            "regex": "/\\*",
            "next": "state_8"
        },
        {
            "token": [
                null,
                "comment.line.double-slash.sass"
            ],
            "regex": "//",
            "next": "state_9"
        },
        {
            "token": [
                "entity.other.attribute-name.tag.pseudo-class",
                "entity.other.attribute-name.tag.pseudo-class"
            ],
            "regex": "\\+[-\\w]+"
        },
        {
            "token": [
                "constant.numeric.sass",
                "constant.numeric.sass"
            ],
            "regex": "(-|\\+)?\\s*[0-9]+(\\.[0-9]+)?"
        },
        {
            "token": [
                "constant.string.sass",
                "constant.string.sass"
            ],
            "regex": "(left|right|true|false|top|bottom)(?!:)"
        },
        {
            "token": [
                "constant.other.unit.sass",
                "constant.other.unit.sass"
            ],
            "regex": "(?<=[\\d])(px|pt|cm|mm|in|em|ex|pc)\\b|%", // ERROR: This contains a lookbehind, which JS does not support :("
        },
        {
            "token": [
                null,
                "variable.parameter.url"
            ],
            "regex": "url",
            "next": "state_14"
        },
        {
            "token": [
                "keyword.control.untitled",
                "keyword.control.untitled"
            ],
            "regex": "&"
        },
        {
            "token": [
                "entity.other.attribute-name.tag.pseudo-class",
                "entity.other.attribute-name.tag.pseudo-class"
            ],
            "regex": ":(before|after|first-child|last-child|first-line|first-letter|link|focus|lang|hover|active|visited)"
        },
        {
            "token": [
                "support.type.property-name.sass",
                "support.type.property-name.sass"
            ],
            "regex": "\\b(azimuth|background-attachment|background-color|background-image|background-position|background-repeat|background|border-bottom-color|border-bottom-style|border-bottom-width|border-bottom|border-collapse|border-color|border-left-color|border-left-style|border-left-width|border-left|border-right-color|border-right-style|border-right-width|border-right|border-spacing|border-style|border-top-color|border-top-style|border-top-width|border-top|border-width|border|bottom|caption-side|clear|clip|color|content|counter-increment|counter-reset|cue-after|cue-before|cue|cursor|direction|display|elevation|empty-cells|float|font-family|font-size-adjust|font-size|font-stretch|font-style|font-variant|font-weight|font|height|left|letter-spacing|line-height|list-style-image|list-style-position|list-style-type|list-style|margin-bottom|margin-left|margin-right|margin-top|marker-offset|margin|marks|max-height|max-width|min-height|min-width|-moz-border-radius|orphans|outline-color|outline-style|outline-width|outline|overflow|padding-bottom|padding-left|padding-right|padding-top|padding|page-break-after|page-break-before|page-break-inside|page|pause-after|pause-before|pause|pitch-range|pitch|play-during|position|quotes|richness|right|size|speak-header|speak-numeral|speak-punctuation|speech-rate|speak|stress|table-layout|text-align|text-decoration|text-indent|text-shadow|text-transform|top|unicode-bidi|vertical-align|visibility|voice-family|volume|white-space|widows|width|word-spacing|z-index)\\b"
        },
        {
            "token": [
                "support.constant.property-value.sass",
                "support.constant.property-value.sass"
            ],
            "regex": "\\b(absolute|all-scroll|always|auto|baseline|below|bidi-override|block|bold|bolder|both|bottom|break-all|break-word|capitalize|center|char|circle|col-resize|collapse|crosshair|dashed|decimal|default|disabled|disc|distribute-all-lines|distribute-letter|distribute-space|distribute|dotted|double|e-resize|ellipsis|fixed|groove|hand|help|hidden|horizontal|ideograph-alpha|ideograph-numeric|ideograph-parenthesis|ideograph-space|inactive|inherit|inline-block|inline|inset|inside|inter-ideograph|inter-word|italic|justify|keep-all|left|lighter|line-edge|line-through|line|list-item|loose|lower-alpha|lower-roman|lowercase|lr-tb|ltr|medium|middle|move|n-resize|ne-resize|newspaper|no-drop|no-repeat|nw-resize|none|normal|not-allowed|nowrap|oblique|outset|outside|overline|pointer|progress|relative|repeat-x|repeat-y|repeat|right|ridge|row-resize|rtl|s-resize|scroll|se-resize|separate|small-caps|solid|square|static|strict|super|sw-resize|table-footer-group|table-header-group|tb-rl|text-bottom|text-top|text|thick|thin|top|transparent|underline|upper-alpha|upper-roman|uppercase|vertical-ideographic|vertical-text|visible|w-resize|wait|whitespace)\\b"
        },
        {
            "token": [
                "support.constant.font-name.sass",
                "support.constant.font-name.sass"
            ],
            "regex": "(\\b(?i:arial|century|comic|courier|garamond|georgia|helvetica|impact|lucida|symbol|system|tahoma|times|trebuchet|utopia|verdana|webdings|sans-serif|serif|monospace)\\b)"
        }
    ],
    "state_3": [
        {
            "token": "constant.character.escaped.sass",
            "regex": "\\\\."
        },
        {
            "token": "TODO",
            "regex": "\"",
            "next": "start"
        }
    ],
    "state_8": [
        {
            "token": "TODO",
            "regex": "\\*/|$",
            "next": "start"
        },
        {
            "token": "TODO",
            "regex": ".+",
            "next": "state_8"
        }
    ],
    "state_9": [
        {
            "token": "TODO",
            "regex": "$\\n?",
            "next": "start"
        },
        {
            "token": "TODO",
            "regex": ".+",
            "next": "state_9"
        }
    ],
    "state_14": [
        {
            "token": "TODO",
            "regex": "\\)",
            "next": "start"
        },
        {
            "token": "TODO",
            "regex": ".+",
            "next": "state_14"
        }
    ]
}
};

oop.inherits(SASSHighlightRules, TextHighlightRules);

exports.SASSHighlightRules = SASSHighlightRules;
});