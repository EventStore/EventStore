import {ContainerPluginOptions} from "@vuepress/plugin-container";

export default function(name, tag, attr): [string, ContainerPluginOptions] {
    return [
        "@vuepress/container",
        {
            type: name,
            before: x => `<${tag}${attr ? " " + attr(x) : ""}>`,
            after: _ => `</${tag}>`,
        },
    ];
}
