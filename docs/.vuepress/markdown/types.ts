import type {PageFrontmatter, PageHeader} from "vuepress";

export type MarkdownHeader = PageHeader;

export interface MarkdownLink {
    raw: string;
    relative: string;
    absolute: string;
}

export interface MarkdownEnv {
    base?: string;
    filePath?: string | null;
    filePathRelative?: string | null;
    frontmatter?: PageFrontmatter;
    headers?: MarkdownHeader[];
    hoistedTags?: string[];
    importedFiles?: string[];
    links?: MarkdownLink[];
    title?: string;
}

type Nesting = 1 | 0 | -1;

export interface MdToken {
    type: string;
    tag: string;
    attrs: Array<[string, string]> | null;
    map: [number, number] | null;
    nesting: Nesting;
    level: number;
    attrSet(name: string, value: string): void;
    attrGet(name: string): string | null;
}

export interface MdEnv {
    base: string;
    filePath: string;
    filePathRelative: string;
}
