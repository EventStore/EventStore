function renderHtmlBy(data, templateJs) {

    $.when($.get(templateJs))
        .done(function(template) {
            $.templates({ renderTemplate: template });
            $('#data').html($.render.renderTemplate(data,
                {
                    formatDate: function(s) {
                        var d = new Date(s);

                        return (1900 + d.getYear()) + "-" + (1 + d.getMonth()) + "-" + (1 + d.getDate()) + " " +
                            d.getHours() + ":" + d.getMinutes() + ":" + d.getSeconds();
                    }
                }
            ));

        });
}