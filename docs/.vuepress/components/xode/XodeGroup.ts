import { defineComponent, h, onBeforeUpdate, ref, watch } from 'vue';
import type { Component, VNode } from 'vue';
import store from "./store";

declare const __VUEPRESS_DEV__: boolean;

export default defineComponent({
  name: 'CodeGroup',

  setup(_, { slots }) {
    // index of current active item
    const activeIndex = ref(-1);

    // refs of the tab buttons
    const tabRefs = ref<HTMLButtonElement[]>([])

    if (__VUEPRESS_DEV__) {
      // after removing a code-group-item, we need to clear the ref
      // of the removed item to avoid issues caused by HMR
      onBeforeUpdate(() => {
        tabRefs.value = []
      })
    }

    const activate = (index: number): void => {
      if (tabRefs.value.length === 0) return;
      let i = index;
      if (i < 0) i = 0;
      if (i >= tabRefs.value.length) i = tabRefs.value.length - 1;
      activeIndex.value = i;
    }

    // handle keyboard event
    const keyboardHandler = (event: KeyboardEvent): void => {
      if (event.key === 'ArrowRight') {
        event.preventDefault();
        activate(activeIndex.value + 1);
      } else if (event.key === 'ArrowLeft') {
        event.preventDefault();
        activate(activeIndex.value - 1);
      }
    }

    const getItems = (vNodes) => vNodes.filter(vNode => (vNode.type as Component).name === 'CodeGroupItem');

    const tabs = getItems(slots.default?.() || []).map(tab => tab.props.title);

    watch(activeIndex, (index, prevIndex) => {
      if (index !== prevIndex)
        store.changeLanguage(tabs[index]);
    });
    watch(() => store.state.codeLanguage, (lang, prev) => {
      if (lang === prev) return;
      const index = tabs.indexOf(lang);
      activate(index);
    });

    return () => {
      // NOTICE: here we put the `slots.default()` inside the render function to make
      // the slots reactive, otherwise the slot content won't be changed once the
      // `setup()` function of current component is called

      // get children code-group-item
      const items = getItems(slots.default?.() || [])
        .map(vNode => {
          if (vNode.props === null) {
            vNode.props = {};
          }
          return vNode as VNode & { props: Exclude<VNode['props'], null> };
        });

      // do not render anything if there is no code-group-item
      if (items.length === 0) {
        return null;
      }

      if (activeIndex.value < 0 || activeIndex.value > items.length - 1) {
        // if `activeIndex` is invalid
        // find the index of the code-group-item with `active` props
        activeIndex.value = items.findIndex(
          vNode => vNode.props.active === '' || vNode.props.active === true
        );

        // if there is no `active` props on code-group-item, set the first item active
        if (activeIndex.value === -1) {
          activeIndex.value = 0;
        }
      } else {
        // set the active item
        items.forEach((vNode, i) => {
          vNode.props.active = i === activeIndex.value;
        })
      }

      return h('div', { class: 'code-group' }, [
        h(
          'div',
          { class: 'code-group__nav' },
          h(
            'ul',
            { class: 'code-group__ul' },
            items.map((vNode, i) => {
              const isActive = i === activeIndex.value

              return h(
                'li',
                { class: 'code-group__li' },
                h(
                  'button',
                  {
                    ref: (element) => {
                      if (element) {
                        tabRefs.value[i] = element as HTMLButtonElement
                      }
                    },
                    class: {
                      'code-group__nav-tab': true,
                      'code-group__nav-tab-active': isActive,
                    },
                    ariaPressed: isActive,
                    ariaExpanded: isActive,
                    onClick: () => (activeIndex.value = i),
                    onKeydown: (e) => keyboardHandler(e),
                  },
                  vNode.props.title
                )
              )
            })
          )
        ),
        items,
      ])
    }
  },
})
