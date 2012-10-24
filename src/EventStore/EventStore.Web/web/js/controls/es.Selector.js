if (!window.es) { window.es = {}; };
es.Selector = function (opts) {

    var appendToSelector = opts.appendToSelector;
    var getTargetElems = opts.getTargetElems;
    var amendElem = opts.amendElem || function () { };
    var onCheck = opts.onCheck;
    var onUncheck = opts.onUncheck;

    this.updateValue = updateValue;

    var self = this;
    var checkBoxes = null;
    var className = 'selector-item';

    //todo: handle new elements in getTargetElems

    init();

    function init() {

        getTargetElems().each(function () {
            var targetElem = this;
            var li = $("<li class='" + className + "'></li>")
                .append('<label class="checkbox"><input type="checkbox" checked/>' + this.asZoomable().title + '</label>')
                .appendTo(appendToSelector);
            $("input[type='checkbox']", li)
                .change(function () {
                    onCheckedChanged(targetElem, this.checked);
                });

            amendElem.apply(this, [self]);
        });

        $(appendToSelector + " .es-selector-all a").click(function (ev) {
            ev.preventDefault();

            var checkedAttr = $(this).attr("data-show");
            if (typeof checkedAttr == "undefined")
                return;

            var checked = checkedAttr === 'true';
            getTargetElems().each(function () {
                updateValue(this, checked);
            });

        });

        var checkboxSelector = [appendToSelector, " .", className, " input[type='checkbox']"].join('');
        checkBoxes = $(checkboxSelector);
    }


    function updateValue(domElem, checked) {
        var checkbox = getCheckbox(domElem);
        checkbox.attr('checked', checked);
        onCheckedChanged(domElem, checked);
    }

    function onCheckedChanged(domElem, checked) {
        if (checked) {
            onCheck(domElem);
        } else {
            onUncheck(domElem);
        }
    }

    function getCheckbox(domElem) {
        var index = getTargetElems().index(domElem); //wont work with new elems in gettargetElems
        if (index == -1)
            return null; // ?
        var checkbox = checkBoxes.eq(index);
        return checkbox;
    }
};
