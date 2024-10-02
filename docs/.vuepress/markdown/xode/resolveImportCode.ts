import {fs} from "vuepress/utils";
import type {MarkdownEnv} from "../types";
import type {ImportCodeTokenMeta} from "./types";
import {resolveVersionedPath} from "../resolver";

function testLine(line: string, regexp: RegExp, regionName: string, end = false) {
    const [full, tag, name] = regexp.exec(line.trim()) || [];
    return (
        full
        && tag
        && name === regionName
        && tag.match(end ? /^[Ee]nd ?[rR]egion$/ : /^[rR]egion$/)
    );
}

function findRegion(lines: string[] | null, regionName: string) {
    if (lines === null) return undefined;
    const regionRegexps = [
        /^\/\/ ?#?((?:end)?region) ([\w*-]+)$/, // javascript, typescript, java, go
        /^\/\* ?#((?:end)?region) ([\w*-]+) ?\*\/$/, // css, less, scss
        /^#pragma ((?:end)?region) ([\w*-]+)$/, // C, C++
        /^<!-- #?((?:end)?region) ([\w*-]+) -->$/, // HTML, markdown
        /^#(End Region) ([\w*-]+)$/, // Visual Basic
        /^::#(endregion) ([\w*-]+)$/, // Bat
        /^# ?((?:end)?region) ([\w*-]+)$/ // C#, PHP, PowerShell, Python, perl & misc
    ];

    let regexp = null;
    let start = -1;

    for (const [lineId, line] of lines.entries()) {
        if (regexp === null) {
            for (const reg of regionRegexps) {
                if (testLine(line, reg, regionName)) {
                    start = lineId + 1;
                    regexp = reg;
                    break;
                }
            }
        } else if (testLine(line, regexp, regionName, true)) {
            return {start, end: lineId, regexp};
        }
    }

    return null;
}

export const resolveImportCode = (
    {importPath, lineStart, lineEnd, region}: ImportCodeTokenMeta,
    {filePath}: MarkdownEnv
): {
    importFilePath: string | null
    importCode: string
} => {
    const {importFilePath, error} = resolveVersionedPath(importPath, filePath);
    if (importFilePath === null || error !== null){
        return {importFilePath, importCode: error!};
    }

    // read file content
    const fileContent = fs.readFileSync(importFilePath).toString();

    const removeSpaces = (l: string[]) => {
        if (l.length === 0) return l;
        const spaces = l[0].length - l[0].trimStart().length;
        if (spaces === 0) return l;
        return l.map(v => v.substr(spaces));
    }

    const allLines = (fileContent != null) ? fileContent.split('\n') : null;
    if (!allLines) return {importFilePath, importCode: "Code is empty"};
    if (region) {
        const reg = findRegion(allLines, region);
        if (reg) {
            lineStart = reg.start + 1;
            lineEnd = reg.end;
        }
    }

    const lines = allLines
        .slice(lineStart ? lineStart - 1 : lineStart, lineEnd)
        .map(x => x.replace(/\t/g, "    "));
    const code = removeSpaces(lines)
        .join('\n')
        .replace(/\n?$/, '\n');

    // resolve partial import
    return {
        importFilePath,
        importCode: code,
    };
}
