# GameshowPro.Wled
This is a wrapper for [WLED SDK](https://github.com/DevPieter/WLED-SDK). It presents WLED instances in a way consistent with other GameshowPro client libraries.

A websocket connection is made to each WLED instance and updates to each state in info object and maintained with the usual IPropertyChanged notifications.