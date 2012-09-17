$(function ()
{
    var conf = {
        graph: ".chart-cont",
        gcount : $(this.graph).size(),
        gtitle : ".chart-title",
        gbtn_hide: ".hidegraph",

        gcontrol: ".graphcontrol",
        gcontrol_nav: ".graphcontrol_nav"
    };

    $(conf.graph).each(function(index){
        $("<li></li>")
            .append('<label class="checkbox"><input type="checkbox" checked/>'+$(this).find(conf.gtitle).text()+'</label>')
            .appendTo(conf.gcontrol);

        $(this).find(conf.gtitle)
            .append('<a href="" class="hidegraph"><i class="icon-remove"></i></a>');
    });

    $(conf.gcontrol+" input").click(function(){
        var selectgraph = $(conf.gcontrol+" input").index(this);
        if( $(this).is(":checked") ){
            $(conf.graph).eq(selectgraph).show();
        }else{
            $(conf.graph).eq(selectgraph).hide();
        }
    });

    $(conf.gcontrol_nav+" a").click(function(){
        var viewstatus = $(this).data("show");
        $(conf.gcontrol+" input").attr("checked", viewstatus);

        if(viewstatus){
            $(conf.graph).show();
        }else{
            $(conf.graph).hide();
        }

        return false;
    });

    $(conf.graph+" "+conf.gbtn_hide).click(function(){
        var selectgraph = $(conf.graph).index($(this).parents(conf.graph));

        $(conf.graph).eq(selectgraph).hide();

        $(conf.gcontrol+" input").eq(selectgraph).attr("checked", false);
        return false;
    });


});