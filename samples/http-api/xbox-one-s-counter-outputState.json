fromAll()
.when({
    $init: function(){
        return {
            count: 0
        }
    },
    ItemAdded: function(s,e){
        if(e.body.Description.indexOf("Xbox One S") >= 0){
            s.count += 1;
        }
    }
}).outputState()