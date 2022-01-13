import {PluginSimple} from "markdown-it";
import {logger} from "@vuepress/utils";
import {isKnownPlaceholder} from "../../lib/externalPlaceholders";

interface MdEnv {
    base: string;
    filePath: string;
    filePathRelative: string;
}

function replaceCrossLinks(token, env: MdEnv) {
    const href = token.attrGet("href");
    if (!href.startsWith("@")) return;

    const placeholder = href.split("/")[0];
    const known = isKnownPlaceholder(placeholder);

    if (!known) {
        logger.error(`Unable to resolve placeholder ${placeholder} in ${href}, file ${env.filePathRelative}`);
        return;
    }
}

export const replaceLinkPlugin: PluginSimple = (md) => {
    md.core.ruler.after(
        "inline",
        "replace-link",
        (state) => {
            state.tokens.forEach((blockToken) => {
                if (!(blockToken.type === "inline" && blockToken.children)) {
                    return;
                }

                blockToken.children.forEach((token) => {
                    const type = token.type;
                    if (type === "link_open") {
                        replaceCrossLinks(token, state.env);
                    }
                });
            });
        }
    )
}
