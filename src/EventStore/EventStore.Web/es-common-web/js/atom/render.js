function renderHtmlBy(data, templateJs, targetElement) {

    if (!targetElement)
        targetElement = '#data';

    $.when($.get(templateJs))
        .done(function(template) {
            $.templates({ renderTemplate: template });
            var html = $.render.renderTemplate(data,
                {
                    formatDate: function formatDate(s) {

                        function pad(num, size) {
                            var s = num + "";
                            while (s.length < size) s = "0" + s;
                            return s;
                        }


                        var d = new Date(s);
                        return (1900 + d.getYear()) + "-" + pad((1 + d.getMonth()), 2) + "-" + pad(d.getDate(), 2) + " " +
                            pad(d.getHours(), 2) + ":" + pad(d.getMinutes(), 2) + ":" + pad(d.getSeconds(), 2);
                    }
                }
            );
            console.log(html);
            $(targetElement).html(html);

        });
}