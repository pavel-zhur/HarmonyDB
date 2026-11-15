// The original: Pages/Login.razor (with Pages/Login.razor.js, unchanged in public/).
//
// After the first render it imports Login.razor.js and injects the Telegram widget into div#telegram-widget with
// Receiver.Reference; when the identity changes (the widget's login, or the temporary one), it goes to "/".

import { useEffect, useRef } from 'react';
import { configuration, hostEnvironment } from '../configuration';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { getLogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { PageTitle } from '../framework/PageTitle';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { importJsModule, invokeJs, type LoginRazorJs } from '../interop/jsRuntime';
import { services } from '../services/registry';
import { stringAttributes } from '../framework/attributes';
import { errorHandledTask } from '../framework/unhandledExceptions';

// @inject ILogger<Index> Logger
const logger = getLogger('OneShelf.Frontend.Web.Pages.Index');

export function Login() {
    const receiver = services.receiver;
    const identityProvider = services.identityProvider;
    const stateHasChanged = useStateHasChanged();

    // OnInitializedAsync: IdentityProvider.IdentityChange += IdentityProviderOnIdentityChange;
    useEvent(identityProvider.identityChange, () => {
        navigationManager.navigateTo('/');
    });

    // OnAfterRenderAsync(firstRender)
    const rendered = useRef(false);
    useEffect(() => {
        if (rendered.current) return;
        rendered.current = true;

        stateHasChanged();
        void errorHandledTask((async () => {
            const module = await importJsModule<LoginRazorJs>('./Pages/Login.razor.js');
            const telegramBotId = configuration.get('TelegramBotId');
            logger.logInformation(`telegramBotId: ${telegramBotId ?? ''}`);
            await invokeJs<void>(module.InjectWidgetHere, receiver.reference, 'div#telegram-widget', telegramBotId);
        })());
    }, [stateHasChanged, receiver]);

    const tempLogin = async (): Promise<void> => {
        const tempAuth = `{"id":6074255334,"first_name":"Кто-то","username":"none","photo_url":"https://t.me/i/userpic/320/q74ikpgxikYev-rFGCjqQfwDNJHUO-kW7LOzIOdQURY.jpg","auth_date":123,"hash":"${configuration.get('BadHash') ?? ''}"}`;
        await identityProvider.set(tempAuth);
        navigationManager.navigateTo('/');
    };

    return (
        <>
            <PageTitle>Вход</PageTitle>

            <h1>Вход</h1>

            <div className="my-hide-standalone d-block d-lg-none mb-3">Чтобы шкаф работал не в браузере, а как приложение офлайн, <a href="/how-to-install">установите его на телефон</a>.</div>

            <div>
                <p>Привет! Тут можно:</p>{' '}
                <ul>
                    <li>Искать и добавлять любые песни и тональности,</li>{' '}
                    <li>Выбирать свои любимые,</li>{' '}
                    <li>Доступно без интернета,</li>{' '}
                    <li>С телефона и с компьютера,</li>{' '}
                    <li>Включать упрощенные аккорды, смотреть аппликатуры,</li>{' '}
                    <li>Искать прогрессии,</li>{' '}
                    <li>Управлять шортлистами,</li>{' '}
                    <li>Генерить книжки,</li>{' '}
                    <li>Менять бемоли на диезы, итд.</li>{' '}
                    <li>
                        {' '}В клубах (чтобы присоединиться, попросите своего админа):{' '}
                        <ul>
                            <li>Глядеть шортлисты, списки, тональности друзей.</li>
                        </ul>
                    </li>
                </ul>{' '}
                <p>Пожалуйста, войдите через телеграм.</p>
            </div>

            <div id="telegram-widget"></div>

            <div>
                <p className="mt-1">Сначала авторизуйтесь этой кнопкой, вам придёт сообщение в телеграме с вопросом о том, хотите ли вы войти сюда через ваш телеграм аккаунт. <span ref={stringAttributes({ style: 'font-weight: 600' })}>Нажмите в телеграме "разрешить" и возвращайтесь сюда.</span></p>{' '}
                <p className="opacity-75">Иногда <b>на айфонах</b> вы увидите красненькое <small className="text-danger">Server error.</small> Просто пробуйте подтверждать еще раз и возвращайтесь в приложение, всё получится.</p>{' '}
                <p className="opacity-75">Если что-то не работает &mdash; <a href="https://t.me/pavel_zhur">пишите мне :)</a>.</p>{' '}
                <p>Открытый исходный код, <a target="_blank" href="https://github.com/pavel-zhur/HarmonyDB">https://github.com/pavel-zhur/HarmonyDB</a>.</p>
            </div>

            {hostEnvironment.environment.startsWith('Development') && (
                <span>Чтобы войти без телеграма (при локальной разработке вход не работает), <a href="#" onClick={blazorEventHandler(stateHasChanged, tempLogin)}>нажмите здесь</a>. Этот &lt;span&gt; не виден на продакшне :)</span>
            )}
        </>
    );
}
