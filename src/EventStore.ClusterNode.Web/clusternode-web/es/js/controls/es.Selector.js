if (!window.es) { window.es = {}; };
es.Selector = function (opts) {

    var appendToSelector = opts.appendToSelector;
    var getTargetElems = opts.getTargetElems;
    var amendElem = opts.amendElem || function () { };
    var onCheck = opts.onCheck;
    var onUncheck = opts.onUncheck;

    this.addNewElem = addNewElem;
    this.updateValue = updateValue;
    this.isChecked = isChecked;

    var self = this;
    var checkBoxes = $();
    var className = 'selector-item';

    //note: if new element appears you should add it manually from outside by using 'addNewElem'

    init();

    function init() {

        $.each(getTargetElems(), createCheckboxForElem);

        $(appendToSelector + " .es-selector-all a").click(function (ev) {
            ev.preventDefault();
            ev.stopPropagation();

            var checkedAttr = $(this).attr("data-show");
            if (typeof checkedAttr == "undefined")
                return false;

            var checked = checkedAttr === 'true';
            $.each(getTargetElems(), function () {
                updateValue(this, checked);
            });

            return false;
        });
    }

    function addNewElem(elem) {
        createCheckboxForElem.apply(elem);
    }

    function createCheckboxForElem() {
        var targetElem = this;
        var li = $("<li class='" + className + "'></li>")
                .append('<label class="checkbox"><input type="checkbox" checked/>' + targetElem.asSelectable().title + '</label>')
                .appendTo(appendToSelector);
        var checkBox = $("input[type='checkbox']", li)
                .change(function () {
                    onCheckedChanged(targetElem, this.checked);
                });

        amendElem.apply(this, [self]);

        checkBoxes = checkBoxes.add(checkBox);
    }

    function updateValue(elem, checked) {
        var checkbox = getCheckbox(elem);
        checkbox.attr('checked', checked);
        onCheckedChanged(elem, checked);
    }

    function onCheckedChanged(elem, checked) {
        if (checked) {
            onCheck(elem);
        } else {
            onUncheck(elem);
        }
    }

    function getCheckbox(elem) {

        var index = $.inArray(elem, getTargetElems());
        if (index == -1)
            return null; // ?
        var checkbox = checkBoxes.eq(index);
        return checkbox;
    }

    function isChecked(elem) {
        var checkbox = getCheckbox(elem);
        return checkbox.is(":checked");
    }
};
