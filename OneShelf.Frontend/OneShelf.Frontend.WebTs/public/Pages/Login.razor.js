export function InjectWidgetHere(objRef, el, botId) {

    window.onTelegramAuth = function (user) {
        objRef.invokeMethodAsync('OnTelegramAuth', user);
    }

    var tag = document.createElement('script');
    tag.src = 'https://telegram.org/js/telegram-widget.js?22';
    tag.setAttribute('data-telegram-login', botId);
    tag.setAttribute('data-size', 'large');
    tag.setAttribute('data-onauth', 'onTelegramAuth(user)');
    tag.type = "text/javascript";

    document.querySelector(el).appendChild(tag);
}