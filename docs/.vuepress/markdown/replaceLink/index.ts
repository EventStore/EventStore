import {type PluginWithOptions} from "markdown-it";
import {logger} from "vuepress/utils";
import {isKnownPlaceholder} from "../../lib/externalPlaceholders";
import type {MarkdownEnv, MdToken} from "../types";

export interface ReplaceLinkPluginOptions {
    replaceLink?: (link: string, env: any) => string;
}

function replaceCrossLinks(token: MdToken, env: MarkdownEnv) {
    const href = token.attrGet("href");
    if (href === null) return;
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
            if (opts?.replaceLink === undefined) return;
            state.tokens.forEach((blockToken) => {
                if (!(blockToken.type === "inline" && blockToken.children)) {
                    return;
                }

                const replaceAttr = (token: MdToken, attrName: string) => token.attrSet(attrName, opts.replaceLink!(token.attrGet(attrName)!, state.env));

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
