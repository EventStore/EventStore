import {containerPlugin} from "@vuepress/plugin-container";
import type {Plugin} from "vuepress";

type getAttr = (t: string) => string;

export default function (name: string, tag: string, attr: getAttr): Plugin {
    return containerPlugin(
        {
            type: name,
            before: x => `<${tag}${attr ? " " + attr(x) : ""}>`,
            after: _ => `</${tag}>`,
        });
}
