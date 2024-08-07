<template>
  <div
          class="code-group-item"
          :class="{ 'code-group-item__active': active }"
          :aria-selected="active"
  >
    <slot/>
  </div>
</template>

<script lang="ts">
import {onMounted, watch} from "vue";
import store from "./store";

export default {
    name: 'CodeGroupItem',
    props: {
        title: {
            type: String,
            required: true,
        },
        active: {
            type: Boolean,
            required: false,
            default: false,
        },
    },
    setup(_, {slots}) {
        const findAndReplace = (node, key, replaceTo) => {
            if (replaceTo === "" || key === undefined) return false;
            for (const x of node) {
                if (x.children !== undefined && typeof x.children === "string") {
                    if (x.children.indexOf(key) !== -1) {
                        x.el.innerHTML = x.children.replace(key, replaceTo);
                    }
                } else if (x.children !== null && x.children.length) {
                    findAndReplace(x.children, key, replaceTo);
                }
            }
        }

        const apply = () => {
            findAndReplace(slots.default(), "{connectionString}", store.state.connectionString);
        };
        onMounted(() => apply());
        watch(store.state, () => apply())
    }
}
</script>
