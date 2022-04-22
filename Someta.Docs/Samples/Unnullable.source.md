# Unnullable

While working on a Blazor website, I found I had a situation where I wanted to bind properties to parameters (as one does in Blazor) but wanted to make it so that the value of the property can never be set to null. If a null value were to be passed, I wanted to ignore it and allow the property to retain its old value. (this was due to a complex page that handled a number of different routes, so some of the bound properties would only be bound to a real value for certain routes. so when navigating to some of the other routes, those properties would be set to null. I wanted to retain those values so that when navigating back the route values wouldn't be lost) This turned out to be a perfect use case for Someta:

snippet: Unnullable

Usage:

snippet: UnnullableExample