if (!window.es) { window.es = {}; };
es.Selector = function (opts) {

    var appendToSelector = opts.appendToSelector;
    var getTargetElems = opts.getTargetElems;
    var amendElem = opts.amendElem || function () { };
    var onCheck = opts.onCheck;
    var onUncheck = opts.onUncheck;
    
    this.addNewElem = addNewElem;
    this.updateValue = updateValue;
    
    var self = this;
    var checkBoxes = $();
    var className = 'selector-item';

    //note: if new element appears you should add it manually from outside by using 'addNewElem'

    init();

    function init() {

        getTargetElems().each(createCheckboxForElem);

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
    }
    
    function addNewElem(domElem) {
        createCheckboxForElem.apply(domElem);
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
        var index = getTargetElems().index(domElem);
        if (index == -1)
            return null; // ?
        var checkbox = checkBoxes.eq(index);
        return checkbox;
    }

    
};
