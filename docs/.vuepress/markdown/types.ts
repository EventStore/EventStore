import type { PageFrontmatter, PageHeader } from '@vuepress/shared';

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
