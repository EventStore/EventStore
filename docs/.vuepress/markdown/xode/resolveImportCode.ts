import {fs, path, logger} from "@vuepress/utils";
import type {MarkdownEnv} from "../types";
import type {ImportCodeTokenMeta} from "./types";

function testLine(line, regexp, regionName, end = false) {
    const [full, tag, name] = regexp.exec(line.trim()) || [];
    return (
        full
        && tag
        && name === regionName
        && tag.match(end ? /^[Ee]nd ?[rR]egion$/ : /^[rR]egion$/)
    );
}

function findRegion(lines, regionName) {
    const regionRegexps = [
        /^\/\/ ?#?((?:end)?region) ([\w*-]+)$/, // javascript, typescript, java, go
        /^\/\* ?#((?:end)?region) ([\w*-]+) ?\*\/$/, // css, less, scss
        /^#pragma ((?:end)?region) ([\w*-]+)$/, // C, C++
        /^<!-- #?((?:end)?region) ([\w*-]+) -->$/, // HTML, markdown
        /^#(End Region) ([\w*-]+)$/, // Visual Basic
        /^::#(endregion) ([\w*-]+)$/, // Bat
        /^# ?((?:end)?region) ([\w*-]+)$/ // C#, PHP, Powershell, Python, perl & misc
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
    let importFilePath = importPath;

    if (!path.isAbsolute(importPath)) {
        // if the importPath is relative path, we need to resolve it
        // according to the markdown filePath
        if (!filePath) {
            logger.error(`Unable to resolve code path: ${filePath}`);
            return {
                importFilePath: null,
                importCode: 'Error when resolving path',
            };
        }
        importFilePath = path.resolve(filePath, '..', importPath);
    }

    // check file existence
    if (!fs.existsSync(importFilePath)) {
        logger.error(`Code file can't be found: ${importFilePath}`);
        return {
            importFilePath,
            importCode: 'File not found!',
        };
    }

    // read file content
    const fileContent = fs.readFileSync(importFilePath).toString();

    const removeSpaces = (l: string[]) => {
        if (l.length === 0) return l;
        const spaces = l[0].length - l[0].trimStart().length;
        if (spaces === 0) return l;
        return l.map((v, i) => v.substr(spaces));
    }

    const allLines = fileContent.split('\n');
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
