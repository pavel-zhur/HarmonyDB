// The original: App.razor.
//
//   <ApplicationInsightsInit IsWasmStandalone="true" />
//   <Router AppAssembly="@typeof(App).Assembly">
//       <Found Context="routeData">
//           <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
//           <FocusOnNavigate RouteData="@routeData" Selector="h1" />
//       </Found>
//       <NotFound>
//           <PageTitle>Not found</PageTitle>
//           <LayoutView Layout="@typeof(MainLayout)">
//               <p role="alert">Sorry, there's nothing at this address.</p>
//           </LayoutView>
//       </NotFound>
//   </Router>
//
// The routes are the @page directives of the original's pages. Between pages the layout stays (as RouteView keeps it);
// between a page and "not found" it is created anew (as there, where they are different views). An exception while
// rendering is caught below the router (ComponentErrorBoundary): around the page, and around everything for the layout.

import { useEffect, useLayoutEffect, type ComponentType } from 'react';
import { createBrowserRouter, Outlet, RouterProvider, useLocation, useMatches, type RouteObject } from 'react-router-dom';
import { ComponentErrorBoundary } from './framework/ComponentErrorBoundary';
import { FocusOnNavigate } from './framework/FocusOnNavigate';
import { ApplicationInsightsInit } from './packages/BlazorApplicationInsights/ApplicationInsightsInit';
import { attachRouter, enableNavigationInterception, isUnknownAddressToLoad, loadFromServer, resetScrollIfNeeded } from './framework/navigation';
import { PageTitle } from './framework/PageTitle';
import { Artists } from './pages/Artists';
import { Cache } from './pages/Cache';
import { Categories } from './pages/Categories';
import { Collection } from './pages/Collection';
import { Collective } from './pages/Collective';
import { Fingerings } from './pages/Fingerings';
import { HowToInstall } from './pages/HowToInstall';
import { Illustrations } from './pages/Illustrations';
import { Index } from './pages/Index';
import { Index2 } from './pages/Index2';
import { Login } from './pages/Login';
import { MyCollection } from './pages/MyCollection';
import { Print } from './pages/Print';
import { Progressions } from './pages/Progressions';
import { ReleaseNotes } from './pages/ReleaseNotes';
import { Tuner } from './pages/Tuner';
import { User } from './pages/User';
import { VisitedChords } from './pages/VisitedChords';
import { VisitedSearch } from './pages/VisitedSearch';
import { MainLayout } from './shared/MainLayout';

const pages: [path: string, page: ComponentType][] = [
    ['/', Index],
    ['/artists', Artists],
    ['/cache', Cache],
    ['/categories', Categories],
    ['/collection', Collection],
    ['/collective', Collective],
    ['/fingerings', Fingerings],
    ['/how-to-install', HowToInstall],
    ['/illustrations', Illustrations],
    ['/index2', Index2],
    ['/login', Login],
    ['/mycollection', MyCollection],
    ['/print', Print],
    ['/progressions', Progressions],
    ['/releasenotes', ReleaseNotes],
    ['/tuner', Tuner],
    ['/user', User],
    ['/visitedchords', VisitedChords],
    ['/visitedsearch', VisitedSearch],
];

const pageRoutes: RouteObject[] = pages.map(([path, Page]) => ({ id: path, path, element: <Page /> }));

const router = createBrowserRouter([
    {
        element: <RouterRoot />,
        children: [
            { element: <Found />, children: pageRoutes },
            { path: '*', element: <NotFound /> },
        ],
    },
]);

attachRouter(router, pageRoutes);

export function App() {
    return (
        <>
            <ApplicationInsightsInit isWasmStandalone={true} />
            <RouterProvider router={router} />
        </>
    );
}

function RouterRoot() {
    const location = useLocation();

    useEffect(() => enableNavigationInterception(), []);

    useLayoutEffect(() => {
        resetScrollIfNeeded();
    }, [location]);

    return (
        <ComponentErrorBoundary location={location}>
            <Outlet />
        </ComponentErrorBoundary>
    );
}

function Found() {
    const matches = useMatches();
    const page = matches[matches.length - 1]?.id;
    const location = useLocation();

    useEffect(() => {
        // After a render at a new address with a #hash, its element is scrolled to (Blazor's router does so).
        if (location.hash.length > 1) {
            document.getElementById(location.hash.substring(1))?.scrollIntoView();
        }
    }, [location.pathname, location.search, location.hash]);

    return (
        <>
            <MainLayout body={<ComponentErrorBoundary location={location}><Outlet /></ComponentErrorBoundary>} />
            <FocusOnNavigate page={page} selector="h1" />
        </>
    );
}

function NotFound() {
    const toLoad = isUnknownAddressToLoad();

    useEffect(() => {
        if (toLoad) loadFromServer();
    }, [toLoad]);

    if (toLoad) return null;

    return (
        <>
            <PageTitle>Not found</PageTitle>
            <MainLayout body={<p role="alert">Sorry, there's nothing at this address.</p>} />
        </>
    );
}
