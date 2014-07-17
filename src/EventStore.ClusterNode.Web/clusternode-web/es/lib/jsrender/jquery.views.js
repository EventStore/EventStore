/*! JsViews v1.0pre: http://github.com/BorisMoore/jsviews */
/*
* Interactive data-driven views using templates and data-linking.
* Requires jQuery, and jsrender.js (next-generation jQuery Templates, optimized for pure string-based rendering)
*    See JsRender at http://github.com/BorisMoore/jsrender
*
* Copyright 2012, Boris Moore
* Released under the MIT License.
*/
// informal pre beta commit counter: 21

(function(global, $, undefined) {
	// global is the this object, which is window when running in the usual browser environment.
	// $ is the global var jQuery

	if (!$) {
		// jQuery is not loaded.
		throw "requires jQuery"; // for Beta (at least) we require jQuery
	}

	if ($.link) return; // JsViews is already loaded

	//========================== Top-level vars ==========================

	var versionNumber = "v1.0pre",

		LinkedView, activeBody, $view, rTag, delimOpenChar0, delimOpenChar1, delimCloseChar0, delimCloseChar1, deferChar,
		document = global.document,
		$views = $.views,
		$extend = $.extend,
		$viewsSub = $views.sub,
		FALSE = false, TRUE = true, NULL = null, CHECKBOX = "checkbox",
		topView = $views.View($views.helpers),
		$isArray = $.isArray,
		$templates = $views.templates,
		$observable = $.observable,
		$viewsLinkAttr = "data-link",
		jsvDataStr = "_jsvData",
		linkStr = "link",
		viewStr = "view",
		propertyChangeStr = "propertyChange",
		arrayChangeStr = "arrayChange",
		elementChangeStr = "change.jsv",
		fnSetters = {
			value: "val",
			input: "val",
			html: "html",
			text: "text"
		},
		valueBinding = { from: { fromAttr: "value" }, to: { toAttr: "value"} },
		oldCleanData = $.cleanData,
		oldJsvDelimiters = $views.delimiters,
		error = $views.error,
		rStartTag = /^jsvi|^jsv:/,
		rFirstElem = /^\s*<(\w+)[>\s]/;

	//========================== Top-level functions ==========================

	//===============
	// event handlers
	//===============

	function elemChangeHandler(ev) {
		var setter, cancel, fromAttr, linkContext, sourceValue, cnvtBack, target, $source, view, context, onBeforeChange,
			source = ev.target,
			to = jsViewsData(source);

		if (to = to && to.to) {
			$source = $(source);
			view = $view(source);
			context = view.ctx;
			onBeforeChange = context.onBeforeChange;
			fromAttr = defaultAttr(source);
			setter = fnSetters[fromAttr];
			sourceValue = $.isFunction(fromAttr) ? fromAttr(source) : setter ? $source[setter]() : $source.attr(fromAttr);

			if ((!onBeforeChange || !(cancel = onBeforeChange.call(view, ev) === FALSE)) && sourceValue !== undefined) {
				cnvtBack = $views.converters[to[2]];
				target = to[0];
				to = to[1];
				linkContext = {
					src: source,
					tgt: target,
					cnvtBack: cnvtBack,
					path: to
				};
				if (cnvtBack) {
					sourceValue = cnvtBack.call(view, sourceValue, linkContext);
				}
				if (sourceValue !== undefined && target) {
					$observable(target).setProperty(to, sourceValue);
					if (context.onAfterChange) {  // TODO only call this if the target property changed
						context.onAfterChange.call(linkContext, ev);
					}
				}
				ev.stopPropagation(); // Stop bubbling
			}
			if (cancel) {
				ev.stopImmediatePropagation();
			}
		}
	}

	function getElementDefaultDisplay(elem) {
		var testElem,
			getComputedStyle = global.getComputedStyle,
			cStyle = elem.currentStyle || getComputedStyle(elem, "");

		if (cStyle.display === "none") {
			testElem = document.createElement(elem.nodeName),
			document.body.appendChild(testElem);
			cStyle = (getComputedStyle ? getComputedStyle(testElem, "") : testElem.currentStyle).display;
			// Consider caching the result as a hash against nodeName
			document.body.removeChild(testElem);
		}
		return cStyle;
	}

	function propertyChangeHandler(ev, eventArgs, linkFn, bind) {
		var attr, setter, changed, sourceValue, css, cancel, tagCtx,
			linkCtx = this,
			source = linkCtx.src,
			target = linkCtx.tgt,
			$target = $(target),
			view = linkCtx.view,
			context = view.ctx,
			onBeforeChange = context.onBeforeChange,
			linkNewContent = eventArgs;

		eventArgs = !bind && eventArgs;
		// TODO for <input data-link="a.b" />
		//Currently the following scenarios do work:
		//$.observable(model).setProperty("a.b", "bar");
		//$.observable(model.a).setProperty("b", "bar");
		// TODO Add support for $.observable(model).setProperty("a", { b: "bar" });

		// TODO call onBeforeChange on data-link initialization.
		//    if ( changed && context.onAfterChange ) {
		//        context.onAfterChange.call( link, ev, eventArgs );
		//    }

		if ((!onBeforeChange || !(eventArgs && onBeforeChange.call(linkCtx, ev, eventArgs) === FALSE))
		// If data changed, the ev.data is set to be the path. Use that to filter the handler action...
		&& !(eventArgs && ev.data !== eventArgs.path))
		// && (!view || view._onDataChanged( eventArgs ) !== FALSE ) // Not currently supported or needed for property change
		{
			sourceValue = linkFn.call(linkCtx, source, view, $views, bind || returnVal);
			// Compiled link expression for linkTag - call renderTag, etc.

			attr = linkCtx.attr || defaultAttr(target, TRUE); // May have been modified by renderTag
			tagCtx = linkCtx.tagCtx;                          // May have been modified by renderTag
			if ($.isFunction(sourceValue)) {
				sourceValue = sourceValue.call(source);
			}
			if (attr === "visible") {
				attr = "css-display";
				sourceValue = sourceValue
				// Make sure we set the correct display style for showing this particular element ("block", "inlin" etc.)
					? getElementDefaultDisplay(target)
					: "none";
			}
			cancel = attr === "none";
			if (eventArgs && tagCtx && tagCtx.tag.onUpdate) {
				cancel = tagCtx.tag.onUpdate.call(tagCtx, ev, eventArgs, linkCtx) === FALSE || cancel;
			}
			if (cancel) {
				return;
			}
			if (css = attr.lastIndexOf("css-", 0) === 0 && attr.substr(4)) {
// Possible optimization for perf on integer values
//				prev = $.style(target, css);
//				if (+sourceValue === sourceValue) {
//					// support using integer data values, e.g. 100 for width:"100px"
//					prev = parseInt(prev);
//				}
//				if (changed = prev !== sourceValue) {
//					$.style(target, css, sourceValue);
//				}
				if (changed = $.style(target, css) !== sourceValue) {
					$.style(target, css, sourceValue);
				}
			} else {
				if (attr === "value") {
					if (target.type === "radio") {
						if (target.value === "" + sourceValue) {
							sourceValue = attr = "checked";
						} else {
							return;
						}
					}
					if (target.type === CHECKBOX) {
						attr = "checked";
						// We will set the "checked" attribute
						sourceValue = sourceValue ? attr : undefined;
						// We will compare this ("checked"/undefined) with the current value
					}
				} else if (view._leafBnd && view._nextNode) {
					var parent = target.parentNode,
						next = target.nextSibling;
					if (next.nodeType === 3) {
						if (next.nodeValue !== sourceValue) {
							parent.insertBefore(document.createTextNode(sourceValue), next);
							parent.removeChild(next);
						}
					} else {
						parent.insertBefore(document.createTextNode(sourceValue), next);
					}
				}
				setter = fnSetters[attr];

				if (setter) {
					if (changed = $target[setter]() !== sourceValue) {
						if (attr === "html") {
							replaceContent($target, sourceValue);
							if (linkNewContent) {
								view.link(source, undefined, target);
								// This is a data-link=html{...} update, so need to link new content
							}
						} else {
							$target[setter](sourceValue);
						}
						if (target.nodeName.toLowerCase() === "input") {
							$target.blur(); // Issue with IE. This ensures HTML rendering is updated.
						}
					}
				} else if (changed = $target.attr(attr) != sourceValue) {
					// Setting an attribute to the empty string or undefined should remove the attribute
					$target.attr(attr, (sourceValue === undefined || sourceValue === "") ? null : sourceValue);
				}
			}

			if (eventArgs && changed && context.onAfterChange) {
				context.onAfterChange.call(linkCtx, ev, eventArgs);
			}
		}
	}

	function replaceContent($elem, html) {
		var l, childView, parentView,
			elem = $elem[0],
			parElVws = jsViewsData(elem, viewStr);
		if (l = parElVws.length) {
			// The element is parent for some views. Since we will remove all nodes associated with those views,
			// must first remove any of those views which are direct children of the view this element is in.
			view = $.view(elem);
			while (l--) {
				childView = parElVws[l];
				if (childView.parent === view) {
					view.removeViews(childView.key);
				}
			}
		}
		// insert new content
		$elem.empty().append(html); // Supply non-jQuery version of this...
		// Using append, rather than html, as workaround for issues in IE compat mode.
		// (Using innerHTML leads to initial comments being stripped)
	}

	function arrayChangeHandler(ev, eventArgs) {
		var context = this.ctx,
			onBeforeChange = context.onBeforeChange;

		if (!onBeforeChange || onBeforeChange.call(this, ev, eventArgs) !== FALSE) {
			this._onDataChanged(eventArgs);
			if (context.onAfterChange) {
				context.onAfterChange.call(this, ev, eventArgs);
			}
		}
	}

	function setArrayChangeLink(view) {
		var handler,
			data = view.data,
			onArrayChange = view._onArrayChange,
			leafBindings = view._leafBnd;

		if (!view._useKey) {
			// This is an array view. (view._useKey not defined => data is array)

			if (onArrayChange) {
				// First remove the current handler if there is one
				$([onArrayChange[1]]).off(arrayChangeStr, onArrayChange[0]);
				view._onArrayChange = undefined;
			}

			if (data) {
				// If this view is not being removed, but the data array has been replaced, then bind to the new data array
				handler = function() {
					if (view.data !== undefined) {
						// If view.data is undefined, do nothing. (Corresponds to case where there is another handler on the same data whose
						// effect was to remove this view, and which happened to precede this event in the trigger sequence. So although this
						// event has been removed now, it is still called since already on the trigger sequence)
						arrayChangeHandler.apply(view, arguments);
					}
				};
				$([data]).on(arrayChangeStr, handler);
				view._onArrayChange = [handler, data];
			}
		}
		if (leafBindings && !data) {
			removeLinkTarget(leafBindings);
		}
	}

	function defaultAttr(elem, to) {
		// to: true - default attribute for setting data value on HTML element; false: default attribute for getting value from HTML element
		// Merge in the default attribute bindings for this target element
		var attr = $views.merge[elem.nodeName.toLowerCase()];
		return attr
			? (to
				? attr.to.toAttr
				: attr.from.fromAttr)
			: to
				? "text" // Default is to bind to innerText. Use html{:...} to bind to innerHTML
				: ""; // Default is not to bind from
	}

	function returnVal(value) {
		return value;
	}

	//===============
	// data-linking
	//===============

	function tmplLink(to, from, context, parentView, prevNode, nextNode, index) {
		return $link(this, to, from, context, parentView, prevNode, nextNode, index);
	}

	function $link(tmplOrLinkTag, to, from, context, parentView, prevNode, nextNode, index) {
		if (!tmplOrLinkTag) return;

		to = to.jquery ? to : $(to); // to is a jquery object or an element or selector
		if (!activeBody) {
			activeBody = document.body;
			$(activeBody).on(elementChangeStr, elemChangeHandler);
		}

		var html, datalink, placeholderParent, targetEl,
		onRender = addLinkAnnotations;

		$.each(to, function() {
			targetEl = this;
			if ("" + tmplOrLinkTag === tmplOrLinkTag) {
				// tmplOrLinkTag is a string: treat as data-link expression.
				return bindDataLinkAttributes(tmplOrLinkTag, targetEl, $view(targetEl), from, context, TRUE);
			}

			parentView = parentView || $view(targetEl);
			if (tmplOrLinkTag.markup !== undefined) {
				// This is a call to template.link()
				if (parentView.link === FALSE) {
					context = context || {};
					context.link = onRender = FALSE; // If link=false, don't allow nested context to switch on linking
				}
				// Set link=false, explicitly, to disable linking within a template nested within a linked template
				if (context && context.target === "replace") {
					placeholderParent = targetEl.parentNode;
					context.target = undefined; // Don't pass on as inherited context
				}

				html = tmplOrLinkTag.render(from, context, parentView, undefined, undefined, undefined, onRender);
				if (placeholderParent) {
					// This is target="replace" mode
					prevNode = targetEl.previousSibling;
					nextNode = targetEl.nextSibling;
					$(targetEl).replaceWith(html);
					targetEl = placeholderParent;
				} else {
					prevNode = nextNode = undefined; // When linking from a template, prevNode and nextNode parameters are ignored
					replaceContent($(targetEl), html);
				}
			} else if (tmplOrLinkTag !== TRUE) {
				return;
			}
			// Link the content of the element, since this is a call to template.link(), or to $(el).link(true, ...),
			parentView.link(from, context, targetEl, prevNode, nextNode, index);
		});
		return to; // Allow chaining, to attach event handlers, etc.
	}

	function bindDataLinkTarget(binding, jsvData, linkCtx, view, linkFn, incremental) {
		// Add data link bindings for a link expression in data-link attribute markup

		function bindFn(object, leafToken) {
			// Binding callback called on each dependent object (parameter) that the link expression depends on.
			// For each path add a propertyChange binding to the leaf object, to trigger the compiled link expression,
			// and upate the target attribute on the target element
			if (cvtBack !== undefined) {
				// If this link is a two-way binding, add the linkTo info to JsViews stored data
				jsvData.to = [object, leafToken, cvtBack];
				// For two-way binding, there should be only one path. If not, will bind to the last one.
			}
			isArray = $isArray(object);
			// Remove any existing binding for the same object and leafToken, so we are left with just one for each dependency
			// Note that {{foo param1 p1=param1 p2=param1*param2//}} will have one dependency on param1 and one on param2.
			// foo will be reevaluated as a whole, (and all its properties recalculated) if any dependency is updated.
			removeLinkTarget(binding, object, leafToken);
			binding.push([object, leafToken, handler]);
			if (isArray) {
				$([object]).on(arrayChangeStr, handler);
			} else if (typeof object === "object") {
				$(object).on(propertyChangeStr, NULL, leafToken, handler);
			}
			return object;
		}

		var isArray, tagCtx,
			cvtBack = linkFn.to,
			handler = function(ev, eventArgs, bind) {
				view.dynCtx = linkCtx.ctx;

				propertyChangeHandler.call(linkCtx, ev, eventArgs, linkFn, bind);

				if (!eventArgs && (tagCtx = linkCtx.tagCtx)) {
					// No eventArgs or incremental:false, so this is the initialization of linkTag binding, and is a custom tag.
					// Extend the binding context, and call onBeforeLink, if declared on the tag.
					if (tagCtx.tag.onBeforeLink) {
						tagCtx.tag.onBeforeLink.call(tagCtx, linkCtx);
					}
					binding.linkCtx = linkCtx;
				}
				view.dynCtx = undefined;
			}

		// Call the handler for initialization and parameter binding
		handler(undefined, incremental, bindFn);
		// Note that until observable deals with managing listeners on object graphs, we can't support changing objects higher up the chain, so there is no reason
		// to attach listeners to them. Even $.observable( person ).setProperty( "address.city", ... ); is in fact triggering propertyChange on the leaf object (address)
	}

	function $unlink(tmplOrLinkTag, to, from) {
		if (!arguments.length) {
			// Call to $.unlink()
			if (activeBody) {
				$(activeBody).off(elementChangeStr, elemChangeHandler);
				activeBody = undefined;
			}
			tmplOrLinkTag = TRUE;
			to = document.body;
		}
		if (to) {
			to = to.jquery ? to : $(to); // to is a jquery object or an element or selector
			if (tmplOrLinkTag === TRUE) {
				// Call to $(el).unlink(true) - unlink element and its content
				$.each(to, function() {
					clean(this);
					var elems = this.getElementsByTagName("*"),
					i = elems.length;
					while (i--) {
						clean(elems[i]);
					}
				});
			} else if (tmplOrLinkTag === undefined) {
				// Call to $(el).unlink()
				$.each(to, function() {
					clean(this);
				});
			} else if ("" + tmplOrLinkTag === tmplOrLinkTag) {
				// Call to $(el).unlink(tmplOrLinkTag ...)
				$.each(to, function() {
					var tokens, binding, tagExpr, convertBack, cbLength, attr, linkTags,
						targetEl = this,
						link = targetEl && jsViewsData(targetEl);

					if (link) {
						linkTags = link.link;
						tmplOrLinkTag = normalizeLinkTag(tmplOrLinkTag, targetEl);
						while (tokens = rTag.exec(tmplOrLinkTag)) { // TODO require } to be followed by whitespace or $, and remove the \}(!\}) option.
							// Iterate over the data-link expressions, for different target attrs
							// tokens: [all, attr, tagExpr, defer, tagName, converter, colon, html, code, params]
							attr = tokens[1];
							tagExpr = tokens[2];
							if (tokens[6]) {
								// TODO include this in the original rTag regex. Also, share this code with $link implementation
								// Only for {:} link"
								if (!attr && (convertBack = /^.*:([\w$]*)$/.exec(tokens[9]))) {
									// two-way binding
									convertBack = convertBack[1];
									if (cbLength = convertBack.length) {
										// There is a convertBack function
										tagExpr = tagExpr.slice(0, -cbLength - 1) + delimCloseChar0; // Remove the convertBack string from expression.
									}
								}
							}
							if (binding = linkTags[attr + tagExpr]) {
								if (removeLinkTarget(binding, from)) {
									// If the binding was to this object, and was therefore removed, remove also the two-way binding, if active
									if (convertBack !== undefined) {
										if (!attr && convertBack !== undefined) {
											link.to = undefined;
										}
									}
								}
							}
						}
					}
				});
			}
//else if (tmplOrLinkTag.markup) {
//	// TODO - unlink the content and the arrayChange, but not any other bindings on the element (if container rather than "replace")
//}
//parentView = parentView || $view(targetEl);
//// This is a call from template.unlink()
//parentView.unlink(from, context, targetEl, prevNode, nextNode, index);
		}
	}

	function removeLinkTarget(bindings, from, path) {
		var binding,
		removed = 0,
		l = bindings.length;
		while (l-- > 0) {
			binding = bindings[l]; // [sourceObject, path, handler]
			if (!(from && from !== binding[0] || path && path !== binding[1])) {
				if ($isArray(binding[0])) {
					$([binding[0]]).off(arrayChangeStr, binding[2]);
				} else {
					$(binding[0]).off(propertyChangeStr, binding[2]);
				}
				bindings.splice(l, 1);
				removed++;
			}
		}
		return removed;
	}

	function normalizeLinkTag(linkMarkup, node) {
		linkMarkup = $.trim(linkMarkup);
		return linkMarkup.charAt(linkMarkup.length - 1) !== "}"
		// If simplified syntax is used: data-link="expression", convert to data-link="{:expression}",
		// or for inputs, data-link="{:expression:}" for (default) two-way binding
			? linkMarkup = delimOpenChar1 + ":" + linkMarkup + (defaultAttr(node) ? ":" : "") + delimCloseChar0
			: linkMarkup;
	}

	function bindDataLinkAttributes(linkMarkup, node, currentView, data, context, incremental) {
		var links, bindings, linkTags, tokens, jsvData, attr, linkIndex, convertBack, cbLength, tagExpr, linkFn, linkTag;

		if (linkMarkup && node) {
			linkIndex = currentView._lnk++;
			// Compiled linkFn expressions are stored in the tmpl.links array of the template
			// TODO - consider also caching globally so that if {{:foo}} or data-link="foo" occurs in different places, the compiled template for this is cached and only compiled once...
			links = currentView.links || currentView.tmpl.links;
			bindings = getOrCreateJsViewsData(node, linkStr);
			jsvData = jsViewsData(node);
			if (!(linkTags = links[linkIndex])) {
				linkTags = links[linkIndex] = {};
				linkMarkup = normalizeLinkTag(linkMarkup, node);
				while (tokens = rTag.exec(linkMarkup)) { // TODO require } to be followed by whitespace or $, and remove the \}(!\}) option.
					// Iterate over the data-link expressions, for different target attrs, e.g. <input data-link="{:firstName:} title{>~description(firstName, lastName)}"
					// tokens: [all, attr, tagExpr, defer, tagName, converter, colon, html, code, params]
					attr = tokens[1];
					tagExpr = tokens[2];
					convertBack = undefined;
					if (tokens[6]) {
						// TODO include this in the original rTag regex
						// Only for {:} link"
						if (!attr && (convertBack = /^.*:([\w$]*)$/.exec(tokens[9]))) {
							// two-way binding
							convertBack = convertBack[1];
							if (cbLength = convertBack.length) {
								// There is a convertBack function
								tagExpr = tagExpr.slice(0, -cbLength - 1) + delimCloseChar0; // Remove the convertBack string from expression.
							}
						}
						if (convertBack === NULL) {
							convertBack = undefined;
						}
					}
					// Compile the linkFn expression which evaluates and binds a data-link expression
					// TODO - optimize for the case of simple data path with no conversion, helpers, etc.:
					//     i.e. data-link="a.b.c". Avoid creating new instances of Function every time. Can use a default function for all of these...
					linkTag = attr + tagExpr;
					linkTags[linkTag] = linkFn = $views._tmplFn(delimOpenChar0 + tagExpr + delimCloseChar1, undefined, TRUE);
					if (!attr && convertBack !== undefined) {
						linkFn.to = convertBack;
					}
					linkFn.attr = attr;
				}
			}
			for (linkTag in linkTags) {
				linkFn = linkTags[linkTag];
				bindDataLinkTarget(
					binding = bindings[linkTag] = bindings[linkTag] || [],
					jsvData,
					{
						linkTag: linkTag,
						src: data || currentView.data, // source
						tgt: node,                     // target
						view: currentView,
						ctx: context,
						attr: linkFn.attr
					},
					currentView,
					linkFn,                    // Link info
					incremental               // Link binding added incrementally (via addLink)
				);
			}
			return bindings;
		}
	}

	function addLinkAnnotations(value, tmpl, props, key) {
		var elemAnnotation, attr,
			tag = tmpl.tag,
			linkInfo = "i",
			closeToken = "/i";

		if (!tag) {
			tag = rFirstElem.exec(tmpl.markup);
			tag = tmpl.tag = (tag || (tag = rFirstElem.exec(value))) && tag[1];
		}

		if (key) {
			linkInfo = (tmpl.bound ? "!" + tmpl.key : "") + ":" + key;
			closeToken = "/t";
		}
		if (/^(option|optgroup|li|tr|td)$/.test(tag)) {
			elemAnnotation = "<" + tag + ' jsvtmpl="';
			return elemAnnotation + linkInfo + '"/>' + $.trim(value) + elemAnnotation + closeToken + '"/>';
		}

		return "<!--jsv" + linkInfo + "-->" + value + "<!--jsv" + closeToken + "-->";
	};

	function renderAndLink(view, index, tmpl, views, data, context, addingViewToParent) {
		var html, prevView, prevNode, linkToNode, linkFromNode,
			elLinked = !view._prevNode,
			parentNode = view.parentElem;

		if (index && ("" + index !== index)) {
			prevView = views[index - 1];
			if (!prevView) {
				return; // If subview for provided index does not exist, do nothing
			}
			prevNode = elLinked ? prevView._after : addingViewToParent ? prevView._nextNode : view._prevNode;
		} else {
			prevNode = elLinked ? (view._preceding || view.parent._preceding) : view._prevNode;
		}

		if (prevNode && prevNode.parentNode !== parentNode) {
			return FALSE;
			// Abandon, since node or view has already been removed, or wrapper element has been inserted between prevNode and parentNode
		}
		html = tmpl.render(data, context, view, !addingViewToParent || index, view._useKey && !addingViewToParent);
		// Pass in self._useKey as test for layout template (which corresponds to when self._useKey>0 and self.data is an array)
		if (prevNode) {
			linkToNode = prevNode.nextSibling;
			$(prevNode).after(html);
			prevNode = prevNode.nextSibling;
		} else {
			linkToNode = parentNode.firstChild;
			$(parentNode).prepend(html);
			prevNode = parentNode.firstChild;
		}
		linkFromNode = prevNode && prevNode.previousSibling;

		// Remove the extra tmpl annotation nodes which wrap the inserted items
		parentNode.removeChild(prevNode);
		parentNode.removeChild(linkToNode ? linkToNode.previousSibling : parentNode.lastChild);

		// Link the new HTML nodes to the data
		view.link(data, context, parentNode, linkFromNode, linkToNode, index);
	}

	function viewLink(data, context, parentNode, prevNode, nextNode, index) {
		var self = this,
			views = self.views,
			body = document.body;

		index = index || 0;

		parentNode = "" + parentNode === parentNode
			? $(parentNode)[0]
			: parentNode.jquery
				? parentNode[0]
				: (parentNode || self.parentElem || body);

		function linkSiblings(parentElem, prev, next, isView) {
			// Link the contents of an element, or the contents of a view.
			// In the case of a view, return the nextSibling following the view.
			var view, node, elem, type, key, parentElViews, nextSibling, onAfterCreate, open, bindings, leafBinding, leafView, elemBindings, linkTmpl, binding, onAfterLink, linkCtx, linkInfo;

			if (!isView) {
				// parentElem is within a view (not the container element of a view), so bind data-link attributes
				bindings = bindDataLinkAttributes(parentElem.getAttribute($viewsLinkAttr), parentElem, self, data, context);
			}

			node = (prev && prev.nextSibling) || parentElem.firstChild;
			while (node && node !== next) {
				type = node.nodeType;
				elem = type === 1 && node;
				nextSibling = node.nextSibling;
				if (elem && (linkInfo = node.getAttribute("jsvtmpl")) || type === 8 && (linkInfo = node.nodeValue.split("jsv")[1])) {
					open = linkInfo.charAt(0) !== "/" && linkInfo;
					if (elem) {
						elem = node.tagName;
						parentElem.removeChild(node);
						node = NULL;
					}
					if (open) {
						// open view
						if (bind = linkInfo.charAt(0) === "!") {
							linkInfo = linkInfo.split(":")
							open = linkInfo[1];

							linkTmpl = self.tmpl.tmpls[parseInt(linkInfo[0].slice(1))];
							leafView = views[open];
							leafBinding = leafView._leafBnd = leafView._leafBnd || [];

							bindDataLinkTarget(leafBinding, leafBinding, {
								linkTag: linkTmp.markup,
								src: leafView.data || data, // source
								tgt: node,                     // target
								view: leafView,
								ctx: context,
								attr: linkTmpl.fn.attr
							}, leafView, linkTmpl.fn, TRUE);
							// bind
						} else {
							open = open.slice(1);
						}
						// If this is a template open, use the key. It it is an item open, use the index, and increment
						key = open || index++;
						parentElViews = parentElViews || getOrCreateJsViewsData(parentElem, viewStr);

						// Extend and initialize the view object created in JsRender, as a JsViews view
						view = views[key];
						if (!view.link) {
							view.parentElem = parentElem;
							$extend(view, LinkedView);

							parentElViews && parentElViews.push(view);

							if (view.parent) {
								if (view._useKey) {
									view.nodes = [];
									view._lnk = 0; // compiled link index.
								}
								setArrayChangeLink(view);
							}
							view._prevNode = node;

							if (view.tmpl.presenter) {
								view.presenter = new view.tmpl.presenter(view.ctx, view);
							}
						}
						if (elem && open) {
							// open tmpl
							view._preceding = nextSibling.previousSibling;
							parentElViews.elLinked = !!elem;
						}
						nextSibling = view.link(undefined, undefined, parentElem, nextSibling.previousSibling);  // Link this view:.
					} else {
						// close view
						self._nextNode = node;
						if (elem && linkInfo === "/i") {
							// This is the case where there is no white space between items.
							// Add a text node to act as marker around template insertion point.
							// (Needed as placeholder when inserting new items following this one).
							parentNode.insertBefore(self._after = document.createTextNode(""), nextSibling);
						}
						if (elem && linkInfo === "/t" && nextSibling && nextSibling.tagName && nextSibling.getAttribute("jsvtmpl")) {
							// This is the case where there is no white space between items.
							// Add a text node to act as marker around template insertion point.
							// (Needed as placeholder when the data array is empty).
							parentNode.insertBefore(document.createTextNode(""), nextSibling);
						}
						if (onAfterCreate = self.ctx.onAfterCreate) { // TODO DATA AND CONTEXT??
							onAfterCreate.call(self, self);
						}
						//	if ((leafBinding = self._leafBnd) && leafBinding.tag.onAfterLink) {
						//		var node = self._prevNode;
						//		while((node = node.nextSibling) && node.nodeType !== 1) {}
						//		leafBinding.tag.onAfterLink(node, self);
						//	}
						return nextSibling; // On completing stepping through contents of a view, return nextSibling
					}
				} else {
					if (isView && self.parent && self.nodes) {
						// Add top-level nodes to view.nodes
						self.nodes.push(node);
					}
					if (elem) {
						elemBindings = linkSiblings(elem);
						for (binding in elemBindings) {
							binding = elemBindings[binding];
							if ((linkCtx = binding.linkCtx) && (onAfterLink = linkCtx.tagCtx.tag.onAfterLink)) {
								onAfterLink.call(linkCtx.tagCtx, linkCtx);
							}
						}
					}
				}
				node = nextSibling;
			}
			return bindings; // On completing stepping through the content of an element, return data-link bindings (if any)
		}
		return linkSiblings(parentNode, prevNode, nextNode, TRUE);
	}

	//===============
	// helpers
	//===============

	function jsViewsData(elem, type) {
		var jqData,
			jqDataOnElem = $.cache[elem[$.expando]];

		// Get jqDataOnElem = $.data(elem, jsvData)
		// (Using a faster but more verbose way of accessing the data - for perf optimization, especially on elements not linked by JsViews)
		jqDataOnElem = jqDataOnElem && jqDataOnElem.data;
		jqData = jqDataOnElem && jqDataOnElem[jsvDataStr];
		return type
			? jqData && jqData[type] || []
			: jqData;
	}

	function getOrCreateJsViewsData(elem, type) {
		return (jsViewsData(elem) || $.data(elem, jsvDataStr, { view: [], link: {} }))[type];
	}

	function inputAttrib(elem) {
		return elem.type === CHECKBOX ? elem.checked : $(elem).val();
	}

	function getTemplate(tmpl) {
		// Get nested templates from path
		if ("" + tmpl === tmpl) {
			var tokens = tmpl.split("[");
			tmpl = $templates[tokens.shift()];
			while (tmpl && tokens.length) {
				tmpl = tmpl.tmpls[tokens.shift().slice(0, -1)];
			}
		}
		return tmpl;
	}

	function clean(elem) {
		// Remove data-link bindings, or contained views

		// Note that if we remove an element from the DOM which is a top-level node of a view, this code
		// will NOT remove it from the view.nodes collection. Consider whether we want to support that scenario...

		var l, link, attr, parentView, view, collData,
			jsvData = jsViewsData(elem);

		if (jsvData) {
			// Get links (propertyChange bindings) on this element and unbind
			collData = jsvData.link;
			for (attr in collData) {
				removeLinkTarget(collData[attr]);
			}

			// Get views for which this element is the parentElement, and remove from parent view
			collData = jsvData.view;
			if (l = collData.length) {
				parentView = $view(elem);
				while (l--) {
					view = collData[l];
					if (view.parent === parentView) {
						parentView.removeViews(view.key, undefined, TRUE);
					}
				}
			}
			$.removeData(elem, jsvDataStr);
		}
	}

	//========================== Initialize ==========================

	//=======================
	// JsRender integration
	//=======================

	$viewsSub.onStoreItem = function(store, name, item, process) {
		if (item && store === $templates) {
			item.link = tmplLink;
			if (name) {
				$.link[name] = function() {
					return tmplLink.apply(item, arguments);
				}
			}
		}
	};

	//====================================
	// Additional members for linked views
	//====================================

	LinkedView = {
		refresh: function(context) {
			var index,
				self = this,
				parent = self.parent,
				tmpl = self.tmpl = getTemplate(self.tmpl);

			if (parent) {
				index = !parent._useKey && self.index,
				// Remove HTML nodes
				$(self.nodes).remove(); // Also triggers cleanData which removes child views.
				// Remove child views
				self.removeViews();
				self.nodes = [];

				renderAndLink(self, index, self.tmpl, parent.views, self.data, context);
				setArrayChangeLink(self);
			}
			return self;
		},

		addViews: function(index, dataItems, tmpl) {
			// if view is not an Array View, do nothing
			var viewsCount,
				self = this,
				views = self.views;

			if (!self._useKey && dataItems.length && (tmpl = getTemplate(tmpl || self.tmpl))) {
				// Use passed-in template if provided, since self added view may use a different template than the original one used to render the array.
				viewsCount = views.length + dataItems.length;

				if (renderAndLink(self, index, tmpl, views, dataItems, self.ctx, TRUE) !== FALSE) {
					while (++index < viewsCount) {
						$observable(views[index]).setProperty("index", index);
						// TODO - this is fixing up index, but not key, and not index on child views. Consider changing index to be a getter index(),
						// so we only have to change it on the immediate child view of the Array view, but also so that it notifies all subscriber to #index().
						// Also have a #context() which can be parameterized to give #parents[#parents.length-1].data or #roots[0]
						// to get root data, or other similar context getters. Only create an the index on the child view of Arrays, (whether in JsRender or JsViews)
						// [Otherwise, here, would need to iterate on views[] to set index on children, right down to ArrayViews, which might be too expensive on perf].
					}
				}
			}
			return self;
		},

		removeViews: function(index, itemsCount, keepNodes) {
			// view.removeViews() removes all the child views
			// view.removeViews( index ) removes the child view with specified index or key
			// view.removeViews( index, count ) removes the specified nummber of child views, starting with the specified index
			function removeView(index, parElVws) {
				var i, node, nextNode, nodesToRemove,
					viewToRemove = views[index];
				if (viewToRemove) {
					node = viewToRemove._prevNode;
					nextNode = viewToRemove._nextNode;
					nodesToRemove = node
						? [node]
					// viewToRemove._prevNode is null: this is a view using element annotations, so we will remove the top-level nodes
						: viewToRemove.nodes;

					// If parElVws is passed in, this is an 'Array View', so all child views have same parent element
					// Otherwise, the views are by key, and there may be intervening parent elements, so get parentElViews
					// for each child view that is being removed
					parElVws = parElVws || jsViewsData(viewToRemove.parentElem, viewStr);

					i = parElVws.length;

					if (i) {
						// remove child views of the view being removed
						viewToRemove.removeViews(undefined, undefined, keepNodes);
					}

					// Remove this view from the parentElViews collection
					while (i--) {
						if (parElVws[i] === viewToRemove) {
							parElVws.splice(i, 1);
							break;
						}
					}
					// Remove the HTML nodes from the DOM, unless they have already been removed
					if (!keepNodes) {
						while (node && node.parentNode && node !== nextNode) {
							node = node.nextSibling;
							nodesToRemove.push(node);
						}
						if (viewToRemove._after) {
							nodesToRemove.push(viewToRemove._after);
						}
						$(nodesToRemove).remove();
					}
					viewToRemove.data = undefined;
					setArrayChangeLink(viewToRemove);
				}
			}

			var current, viewsCount, parentElViews,
				self = this,
				isArray = !self._useKey,
				views = self.views;

			if (isArray) {
				viewsCount = views.length;
				parentElViews = jsViewsData(self.parentElem, viewStr);
			}
			if (index === undefined) {
				// Remove all child views
				if (isArray) {
					// views and data are arrays
					current = viewsCount;
					while (current--) {
						removeView(current, parentElViews);
					}
					self.views = [];
				} else {
					// views and data are objects
					for (index in views) {
						// Remove by key
						removeView(index);
					}
					self.views = {};
				}
			} else {
				if (itemsCount === undefined) {
					if (isArray) {
						// The parentView is data array view.
						// Set itemsCount to 1, to remove this item
						itemsCount = 1;
					} else {
						// Remove child view with key 'index'
						removeView(index);
						delete views[index];
					}
				}
				if (isArray && itemsCount) {
					current = index + itemsCount;
					// Remove indexed items (parentView is data array view);
					while (current-- > index) {
						removeView(current, parentElViews);
					}
					views.splice(index, itemsCount);
					if (viewsCount = views.length) {
						// Fixup index on following view items...
						while (index < viewsCount) {
							$observable(views[index]).setProperty("index", index++);
						}
					}
				}
			}
			return this;
		},

		content: function(select) {
			return select ? $(select, this.nodes) : $(this.nodes);
		},

		link: viewLink,

		_onDataChanged: function(eventArgs) {
			if (eventArgs) {
				// This is an observable action (not a trigger/handler call from pushValues, or similar, for which eventArgs will be null)
				var self = this,
					action = eventArgs.change,
					index = eventArgs.index,
					items = eventArgs.items;

				switch (action) {
					case "insert":
						self.addViews(index, items);
						break;
					case "remove":
						self.removeViews(index, items.length);
						break;
					case "move":
						self.refresh(); // Could optimize this
						break;
					case "refresh":
						self.refresh();
						// Othercases: (e.g.undefined, for setProperty on observable object) etc. do nothing
				}
			}
			return TRUE;
		},

		_onRender: addLinkAnnotations
	};

	//=======================
	// Extend $.views namespace
	//=======================

	$extend($views, {
		linkAttr: $viewsLinkAttr,
		merge: {
			input: {
				from: { fromAttr: inputAttrib }, to: { toAttr: "value" }
			},
			textarea: valueBinding,
			select: valueBinding,
			optgroup: {
				from: { fromAttr: "label" }, to: { toAttr: "label" }
			}
		},
		delimiters: function() {
			var delimChars = oldJsvDelimiters.apply(oldJsvDelimiters, arguments);
			delimOpenChar0 = delimChars[0];
			delimOpenChar1 = delimChars[1];
			delimCloseChar0 = delimChars[2];
			delimCloseChar1 = delimChars[3];
			deferChar = delimChars[4];
			rTag = new RegExp("(?:^|\\s*)([\\w-]*)(" + delimOpenChar1 + $views.rTag + ")" + delimCloseChar0 + ")", "g");
			return this;
		}
	});

	//=======================
	// Extend jQuery namespace
	//=======================

	$extend($, {

		//=======================
		// jQuery $.view() plugin
		//=======================

		view: $view = function(node, inner) {
			// $.view() returns top node
			// $.view( node ) returns view that contains node
			// $.view( selector ) returns view that contains first selected element
			var view, parentElViews, i, j, finish, startNode,
				body = document.body;

			if (!node || node === body || topView._useKey < 2) {
				return topView; // Perf optimization for common cases
			}

			startNode = node = "" + node === node
				? $(node)[0]
				: node.jquery
					? node[0]
					: node;

			if (inner) {
				// Treat supplied node as a container element, step through content, and return the first view encountered.
				finish = node.nextSibling || node.parentNode;
				while (finish !== (node = node.firstChild || node.nextSibling || node.parentNode.nextSibling)) {
					if (node.nodeType === 8 && rStartTag.test(node.nodeValue)) {
						view = $view(node);
						if (view._prevNode === node) {
							return view;
						}
					}
				}
				return;
			}

			// Step up through parents to find an element which is a views container, or if none found, store the top-level view on the body
			while (!(parentElViews = jsViewsData(finish = node.parentNode || body, viewStr)).length) {
				if (!finish || node === body) {
					getOrCreateJsViewsData(body, viewStr).push(topView);
					return topView;
				}
				node = finish;
			}

			if (parentElViews.elLinked) {
				i = parentElViews.length;
				while (i--) {
					view = parentElViews[i];
					j = view.nodes && view.nodes.length;
					while (j--) {
						if (view.nodes[j] === node) {
							return view;
						}
					}
				}
			} else while (node) {
				// Step back through the nodes, until we find an item or tmpl open tag - in which case that is the view we want
				if (node === finish) {
					return view;
				}
				if (node.nodeType === 8) {
					if (/^jsv\/[it]$/.test(node.nodeValue)) {
						// A tmpl or item close tag: <!--/tmpl--> or <!--/item-->
						i = parentElViews.length;
						while (i--) {
							view = parentElViews[i];
							if (view._nextNode === node) {
								// If this was the node originally passed in, this is the view we want.
								if (node === startNode) {
									return view;
								}
								// If not, jump to the beginning of this item/tmpl and continue from there
								node = view._prevNode;
								break;
							}
						}
					} else if (rStartTag.test(node.nodeValue)) {
						// A tmpl or item open tag: <!--tmpl--> or <!--item-->
						i = parentElViews.length;
						while (i--) {
							view = parentElViews[i];
							if (view._prevNode === node) {
								return view;
							}
						}
					}
				}
				node = node.previousSibling;
			}
			// If not within any of the views in the current parentElViews collection,
			// move up through parent nodes to and look for view in parentElViews collection
			return $view(finish);
		},

		link: $link,
		unlink: $unlink,

		//=======================
		// override $.cleanData
		//=======================
		cleanData: function(elems) {
			var i = elems.length;

			while (i--) {
				clean(elems[i]);
			}
			oldCleanData.call($, elems);
		}
	});

	$extend($.fn, {
		link: function(expr, from, context, parentView, prevNode, nextNode, index) {
			return $link(expr, this, from, context, parentView, prevNode, nextNode, index);
		},
		unlink: function(expr, from) {
			return $unlink(expr, this, from);
		},
		view: function() {
			return $view(this[0]);
		}
	});

	// Initialize default delimiters
	$views.delimiters();

	$extend(topView, { tmpl: {}, _lnk: 0, links: [] });
	$extend(topView, LinkedView);

})(this, this.jQuery);
