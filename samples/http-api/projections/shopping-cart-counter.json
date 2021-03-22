fromCategory('shoppingCart')
.foreachStream()
.when({
    $init: function(){
        return {
            count: 0
        }
    },
    ItemAdded: function(s,e){
        s.count += 1;
    }
})