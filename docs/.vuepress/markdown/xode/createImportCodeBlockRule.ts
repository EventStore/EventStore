import type {RuleBlock} from "markdown-it/lib/parser_block"
import {path} from "@vuepress/utils"
import {ExtendedCodeImportPluginOptions, ImportCodePluginOptions, ImportCodeTokenMeta, ResolvedImport} from "./types";

// min length of the import code syntax, i.e. '@[code]()'
const MIN_LENGTH = 9;

const startSequence = "@[code";

const knownPrismIssues = {
    "rs": "rust"
};

const replaceKnownPrismExtensions = (ext: string): string => knownPrismIssues[ext] ?? ext;

// regexp to match the import syntax
const SYNTAX_RE = /^@\[code(?:{(\d+)?-(\d+)?})?(?:{(.+)?})?(?: ([^\]]+))?]\(([^)]*)\)/;

export const createImportCodeBlockRule = ({
                                              handleImportPath = (str) => [{ importPath: str }],
                                          }: ExtendedCodeImportPluginOptions): RuleBlock => (
    state,
    startLine,
    endLine,
    silent
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

    const addBlock = (r: ResolvedImport) => {
        const meta: ImportCodeTokenMeta = {
            importPath: r.importPath,
            lineStart: lineStart ? Number.parseInt(lineStart, 10) : 0,
            lineEnd: lineEnd ? Number.parseInt(lineEnd, 10) : undefined,
            region: region
        };

        // create a import_code token
        const token = state.push('import_code', 'code', 0);

        // use user specified info, or fallback to file ext
        token.info = info ?? replaceKnownPrismExtensions(path.extname(meta.importPath).slice(1));
        token.markup = '```';
        token.map = [startLine, startLine + 1];
        // store token meta to be used in renderer rule
        token.meta = meta;
    }

    const addGroup = (r: ResolvedImport) => {
        const token = state.push('container_code-group-item', "CodeGroupItem", 1);
        token.block = true;
        token.attrSet("title", r.label);

        addBlock(r);

        state.push('container_code-group-item', "CodeGroupItem", -1);
    }

    const experiment = resolvedImports.length > 1;
    if (experiment) {
        const token = state.push('container_code-group_open', "div", 1);
        token.block = true;

        for (const resolved of resolvedImports) {
            addGroup(resolved);
        }

        state.push('container_code-group_close', "div", -1);
    } else {
        addBlock(resolvedImports[0]);
    }

    state.line = startLine + 1;

    return true;
}
