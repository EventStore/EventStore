import {path} from "vuepress/utils"
import type {ExtendedCodeImportPluginOptions, ImportCodeTokenMeta, ResolvedImport} from "./types";
// @ts-ignore
import type {RuleBlock} from "markdown-it/lib/parser_block";
// @ts-ignore
import type {StateBlock} from "markdown-it";

// min length of the import code syntax, i.e. '@[code]()'
const MIN_LENGTH = 9;

const startSequence = "@[code";

const knownPrismIssues: Record<string, string> = {
    "rs": "rust"
};

const replaceKnownPrismExtensions = (ext: string): string => knownPrismIssues[ext] ?? ext;

// regexp to match the import syntax
const SYNTAX_RE = /^@\[code(?:{(\d+)?-(\d+)?})?(?:{(.+)?})?(?: ([^\]]+))?]\(([^)]*)\)/;

const name = "tabs";

export const createImportCodeBlockRule = ({
                                              handleImportPath = (str) => [{importPath: str}],
                                          }: ExtendedCodeImportPluginOptions): RuleBlock => (
    state: StateBlock,
    startLine: number,
    endLine: number,
    silent: boolean
): boolean => {
    // if it's indented more than 3 spaces, it should be a code block
    /* istanbul ignore if */
    if (state.sCount[startLine] - state.blkIndent >= 4) {
        return false;
    }

    const pos = state.bMarks[startLine] + state.tShift[startLine];
    const max = state.eMarks[startLine];

    // return false if the length is shorter than min length
    if (pos + MIN_LENGTH > max) return false;

    // check if it's matched the start
    if (state.src.substr(pos, startSequence.length) !== startSequence) return false;

    // check if it's matched the syntax
    const match = state.src.slice(pos, max).match(SYNTAX_RE);
    if (!match) return false;

    // return true as we have matched the syntax
    if (silent) return true;

    const [, lineStart, lineEnd, region, info, importPath] = match;

    const resolvedImports = handleImportPath(importPath);

    const addCodeBlock = (r: ResolvedImport) => {
        const meta: ImportCodeTokenMeta = {
            importPath: r.importPath,
            lineStart: lineStart ? Number.parseInt(lineStart, 10) : 0,
            lineEnd: lineEnd ? Number.parseInt(lineEnd, 10) : undefined,
            region: region
        };

        // create an import_code token
        const token = state.push('import_code', 'code', 0);

        // use user specified info, or fallback to file ext
        token.info = info ?? replaceKnownPrismExtensions(path.extname(meta.importPath).slice(1));
        token.markup = '```';
        token.map = [startLine, startLine + 1];
        // store token meta to be used in renderer rule
        token.meta = meta;
    }

    const addGroupItem = (r: ResolvedImport) => {
        const token = state.push(`${name}_tab_open`, "", 1);
        token.block = true;
        token.info = r.label;
        token.meta = {active: false};

        addCodeBlock(r);

        state.push(`${name}_tab_close`, "", -1);
    }

    const experiment = resolvedImports.length > 1;
    if (experiment) {
        const token = state.push(`${name}_tabs_open`, "", 1);
        token.info = name;
        token.meta = {id: "code"};

        for (const resolved of resolvedImports) {
            addGroupItem(resolved);
        }

        state.push(`${name}_tabs_close`, "", -1);
    } else {
        addCodeBlock(resolvedImports[0]);
    }

    state.line = startLine + 1;

    return true;
}
