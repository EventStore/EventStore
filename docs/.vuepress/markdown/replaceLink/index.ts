import {PluginSimple, PluginWithOptions} from "markdown-it";
import {logger} from "@vuepress/utils";
import {isKnownPlaceholder} from "../../lib/externalPlaceholders";

export interface ReplaceLinkPluginOptions {
    replaceLink?: (link: string, env: any) => string;
}

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

export const replaceLinkPlugin: PluginWithOptions<ReplaceLinkPluginOptions> = (md, opts) => {
    md.core.ruler.after(
        "inline",
        "replace-link",
        (state) => {
            state.tokens.forEach((blockToken) => {
                if (!(blockToken.type === "inline" && blockToken.children)) {
                    return;
                }

                const replaceAttr = (token, attrName) => token.attrSet(attrName, opts.replaceLink(token.attrGet(attrName), state.env));

                blockToken.children.forEach((token) => {
                    const type = token.type;
                    if (type === "link_open") {
                        replaceAttr(token, "href");
                        replaceCrossLinks(token, state.env);
                    }
                });
            });
        }
    )
}
