import {PluginSimple} from "markdown-it";
import {fs, logger, path} from "@vuepress/utils";
import {isKnownPlaceholder} from "../../lib/externalPlaceholders";

interface MdEnv {
    base: string;
    filePath: string;
    filePathRelative: string;
}

function checkLink(token, attrName: string, env: MdEnv) {
    const href = token.attrGet(attrName);
    if (href.startsWith("http")) return;

    if (href.startsWith("@")) {
        const known = isKnownPlaceholder(href.split("/")[0]);
        if (known) return;
    }

    // check the link
    const split = href.split("#");
    const currentPath = href[0] == "/" ? path.resolve(__dirname, "../../..") : path.dirname(env.filePath);
    const p = path.join(currentPath, split[0]);
    fs.stat(p, (err, stat) => {
        if (err != null) {
            logger.error(`Broken link in ${env.filePathRelative}\r\nto: ${split[0]}`);
        }
    });
}

export const linkCheckPlugin: PluginSimple = (md) => {
    md.core.ruler.after(
        "inline",
        "link-check",
        (state) => {
            state.tokens.forEach((blockToken) => {
                if (!(blockToken.type === "inline" && blockToken.children)) {
                    return;
                }

                blockToken.children.forEach((token) => {
                    const type = token.type;
                    switch (type) {
                        case "link_open":
                            checkLink(token, "href", state.env);
                            break;
                        case "image":
                            checkLink(token, "src", state.env)
                            break;
                    }
                })
            })
        }
    )
}
