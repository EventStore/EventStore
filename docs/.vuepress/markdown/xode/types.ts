export interface ImportCodeTokenMeta {
    importPath: string;
    lineStart: number;
    lineEnd?: number;
    region?: string;
}

export interface ImportCodePluginOptions {
    /**
     * A function to handle the import path
     */
    handleImportPath?: (str: string) => string;
}

export interface ResolvedImport {
    label?: string;
    importPath: string;
}

export interface ExtendedCodeImportPluginOptions {
    handleImportPath?: (files: string) => ResolvedImport[];
}
