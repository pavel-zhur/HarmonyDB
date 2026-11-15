// The original: Shared/MainLayout.razor (with Shared/MainLayout.razor.js, unchanged in public/).
// A layout (LayoutComponentBase): the router gives it the page to show as body.
//
// It cascades itself to the page (MainLayoutContext) for SecondarySidebar, whose content it shows in the secondary
// sidebar. After every render of its own it imports MainLayout.razor.js and calls InitSecondToTop; after the first,
// before that, MainLayoutRendered (the theme's App.initCore/initAfterLoad) and JsFunctions.FirstInitSuccessful.

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { MainLayoutContext, type MainLayoutCascade, type SecondarySidebarInstance } from '../components/SecondarySidebar';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { importJsModule, invokeJs, type MainLayoutRazorJs } from '../interop/jsRuntime';
import { services } from '../services/registry';
import { MenuBottom } from './MenuBottom';
import { MenuLeft } from './MenuLeft';
import { MenuTop } from './MenuTop';
import './MainLayout.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

export interface MainLayoutProps {
    body: ReactNode;
}

export function MainLayout({ body }: MainLayoutProps) {
    const jsFunctions = services.jsFunctions;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        secondarySidebar: null as SecondarySidebarInstance | null,
        just: false,
    }).current;

    const [cascade] = useState<MainLayoutCascade>(() => {
        const self: MainLayoutCascade = {
            setSecondarySidebar(secondarySidebar) {
                fields.secondarySidebar = secondarySidebar;
                if (secondarySidebar === null) {
                    self.setSidebar(false);
                }

                self.update();
            },
            setSidebar(just) {
                if (fields.just !== just) {
                    fields.just = just;
                    self.update();
                }
            },
            update() {
                stateHasChanged();
            },
        };

        return self;
    });

    // OnAfterRenderAsync, after every render of its own.
    const firstRender = useRef(true);
    useEffect(() => {
        const isFirstRender = firstRender.current;
        firstRender.current = false;
        void errorHandledTask((async () => {
            const module = await importJsModule<MainLayoutRazorJs>('./Shared/MainLayout.razor.js');
            if (isFirstRender) {
                await invokeJs<void>(module.MainLayoutRendered);
                await jsFunctions.firstInitSuccessful();
            }

            await invokeJs<void>(module.InitSecondToTop);
        })());
    });

    const secondarySidebarContent = fields.secondarySidebar?.childContent ?? null;

    return (
        <>
            <div className={`navbar navbar-dark navbar-expand-lg navbar-static border-bottom border-bottom-white border-opacity-10 my-navbar-black ${fields.just ? 'my-navbar-black-body-just' : ''}`}>
                <MenuTop />
            </div>

            <div className="page-content">

                <MenuLeft />

                {secondarySidebarContent != null && (
                    <div className="sidebar sidebar-secondary d-none d-lg-flex sidebar-expand my-secondary-sidebar">
                        <div className="sidebar-content" id="my-secondary-sidebar-content" style={{ scrollBehavior: 'smooth' }}>
                            {secondarySidebarContent}
                        </div>
                        <div className="btn-to-top2-cc">
                            <div className="btn-to-top2">
                            </div>
                        </div>
                    </div>
                )}

                <div className="content-wrapper my-content-wrapper">

                    <MainLayoutContext.Provider value={cascade}>

                        <div className="content-inner my-content-inner">

                            <div className="content">

                                {body}

                            </div>

                        </div>
                    </MainLayoutContext.Provider>

                    <div className="btn-to-top"><button className="btn btn-secondary btn-icon rounded-pill" type="button"><i className="ph-arrow-up"></i></button></div>

                </div>

            </div>

            <MenuBottom />
        </>
    );
}
