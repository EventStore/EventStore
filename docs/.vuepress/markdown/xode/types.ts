export interface ImportCodeTokenMeta {
    importPath: string;
    lineStart: number;
    lineEnd?: number;
    region?: string;
}

export interface ResolvedImport {
    label?: string;
    importPath: string;
}

export interface ExtendedCodeImportPluginOptions {
    handleImportPath?: (files: string) => ResolvedImport[];
}
