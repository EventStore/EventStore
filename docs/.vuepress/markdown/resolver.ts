import {logger, path} from "vuepress/utils";
import version from "../lib/version";
import {instance} from "../lib/versioning";

export const resolveVersionedPath = (importPath: string, filePath: string | null | undefined) => {
    let importFilePath = importPath;
    let error: string | null = null;

    if (!path.isAbsolute(importPath)) {
        // if the importPath is a relative path, we need to resolve it
        // according to the markdown filePath
        if (!filePath) {
            logger.error(`Unable to resolve code path: ${filePath}`);
            return {
                importFilePath: null,
                error: 'Error when resolving path',
            };
        }
        importFilePath = path.resolve(filePath, '..', importPath);
    }

    // if (importFilePath.includes("{version}")) {
    //     const ver = version.getVersion(filePath!) ?? instance.latestSemver;
    //     if (ver) {
    //         importFilePath = importFilePath.replace("{version}", ver);
    //     }
    // }

    return {importFilePath, error};
}