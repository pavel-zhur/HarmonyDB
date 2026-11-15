// The original: Pages/Index2.razor — a static draft page (the theme's sample markup). Its empty style="" attributes
// are not rendered (React takes style only as an object).

import { SecondarySidebar } from '../components/SecondarySidebar';
import { stringAttributes } from '../framework/attributes';
import { PageTitle } from '../framework/PageTitle';

export function Index2() {
    return (
        <>
            <PageTitle>Index</PageTitle>

            <SecondarySidebar isJust={false}>
                <div className="border-bottom" id="sidebar-search" ref={stringAttributes({ style: '' })}>
                    <div className="sidebar-section-body">
                        <div className="form-control-feedback form-control-feedback-end">
                            <input type="search" className="form-control" placeholder="Search" />
                            <div className="form-control-feedback-icon">
                                <i className="ph-magnifying-glass opacity-50"></i>
                            </div>
                        </div>

                        <div className="btn-group pt-3">
                            <button type="button" className="btn btn-primary active">Новые</button>
                            <button type="button" className="btn btn-primary">Топ</button>

                            <div className="btn-group">
                                <button type="button" className="btn btn-primary dropdown-toggle" data-bs-toggle="dropdown" aria-expanded="false">Еще</button>
                                <div className="dropdown-menu" ref={stringAttributes({ style: '' })}>
                                    <a href="#" className="dropdown-item">Action</a>
                                    <a href="#" className="dropdown-item">Another action</a>
                                    <a href="#" className="dropdown-item">One more action</a>
                                    <div className="dropdown-divider"></div>
                                    <a href="#" className="dropdown-item">Separated line</a>
                                </div>
                            </div>
                        </div>
                        {'\n\n'}
                        <div className="btn-group pt-3 ps-3">
                            <button type="button" className="btn btn-primary btn-labeled btn-labeled-start dropdown-toggle" data-bs-toggle="dropdown" aria-expanded="false">
                                <span className="btn-labeled-icon bg-black bg-opacity-20">
                                    <i className="fas fa-heart my-yellow"></i>
                                </span>
                                {' '}шортлист
                            </button>

                            <button type="button" className="btn btn-icon">
                                <i className="ph-x"></i>
                            </button>
                            <div className="dropdown-menu" ref={stringAttributes({ style: '' })}>
                                <a href="#" className="dropdown-item">Action</a>
                                <a href="#" className="dropdown-item">Another action</a>
                                <a href="#" className="dropdown-item">One more action</a>
                                <div className="dropdown-divider"></div>
                                <a href="#" className="dropdown-item">Separated line</a>
                            </div>
                        </div>

                    </div>
                </div>

                {/* Sidebar content */}
                <div className="sidebar-content">

                    {/* Actions */}
                    <div className="sidebar-section">
                        <div className="sidebar-section-header border-bottom">
                            <span className="fw-semibold">Библиотека</span>
                            <div className="ms-auto">
                                <a href="#sidebar-actions" className="text-reset" data-bs-toggle="collapse" aria-expanded="true">
                                    <i className="ph-caret-down collapsible-indicator"></i>
                                </a>
                            </div>
                        </div>

                        <div className="collapse show" id="sidebar-actions" ref={stringAttributes({ style: '' })}>
                            <div className="sidebar-section-body">
                                <div className="list-group list-group-borderless my-list-1">
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex active">
                                        A second item

                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item{' '}
                                        <span className="badge border border-teal text-teal rounded-pill ms-auto">добавлены</span>
                                    </a>
                                </div>
                            </div>
                        </div>
                    </div>
                    {/* /actions */}
                    {/* Sub navigation */}
                    <div className="sidebar-section">
                        <div className="sidebar-section-header border-bottom">
                            <span className="fw-semibold">Интернет</span>
                            <div className="ms-auto">
                                <a href="#sidebar-navigation" className="text-reset" data-bs-toggle="collapse" aria-expanded="true">
                                    <i className="ph-caret-down collapsible-indicator"></i>
                                </a>
                            </div>
                        </div>

                        <div className="collapse show" id="sidebar-navigation" ref={stringAttributes({ style: '' })}>
                            <div className="sidebar-section-body">
                                <div className="list-group list-group-borderless my-list-2">
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action">
                                        An item{' '}
                                        <span className="">New New New New New New New New New New New New New New New</span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A second item
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A third item{' '}
                                        <span className="badge bg-teal rounded-pill ms-2">
                                            очень хорошие
                                        </span>
                                    </a>
                                    <a href="#" className="list-group-item list-group-item-action d-flex">
                                        A fourth item
                                    </a>
                                </div>
                            </div>
                        </div>
                    </div>
                    {/* /sub navigation */}

                </div>
                {/* /sidebar content */}
            </SecondarySidebar>

            {/* Page header */}
            <div className="page-header bg-indigo bg-opacity-75 my-header my-content-addition">

                <div className="page-header-content d-block">
                    <div className="d-flex">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">79. сурганова и оркестр - корабли</h5>
                        </div>

                        <a href="#bc_top_full" className="d-flex align-items-center text-body py-2 ms-auto collapsed" data-bs-toggle="collapse" aria-expanded="false">
                            <i className="ph-x-circle my-white"></i>
                        </a>
                    </div>

                </div>
            </div>
            {/* /page header */}

            <p className="fw-bold border-bottom pb-2 mb-2">Badges in actionable items</p>
            <p className="mb-3">Be careful with styling the actionable item in active state and using badges with a solid bg color, due to contrast difference. Outline style or semi transparent background is recommended</p>

            <div className="list-group my-list-2">
                <a href="#" className="list-group-item list-group-item-action">
                    An item{' '}
                    <span className="">New New New New New New New New New New New New New New New</span>
                </a>
                <a href="#" className="list-group-item list-group-item-action d-flex active">
                    A second item

                </a>
                <a href="#" className="list-group-item list-group-item-action d-flex">
                    A third item{' '}
                    <span className="badge bg-teal rounded-pill ms-2">
                        очень хорошие
                    </span>
                </a>
                <a href="#" className="list-group-item list-group-item-action d-flex">
                    A fourth item{' '}
                    <span className="badge border border-teal text-teal rounded-pill ms-auto">добавлены</span>
                </a>
            </div>

            <div className="page-header bg-info bg-opacity-75 mt-4 my-header my-content-addition">

                <div className="page-header-content d-block">
                    <div className="d-flex justify-content-between">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">B. Chords55</h5>
                        </div>

                        <div className="d-flex">
                            <a href="#bc_top_full" className="d-flex align-items-center text-body py-2 ms-auto collapsed" data-bs-toggle="collapse" aria-expanded="false">
                                <i className="ph-arrow-left my-white"></i>
                            </a>
                            <a href="#bc_top_full" className="d-flex align-items-center text-body py-2 ms-2 collapsed" data-bs-toggle="collapse" aria-expanded="false">
                                <i className="ph-x-circle my-white"></i>
                            </a>
                        </div>
                    </div>

                </div>
            </div>

            <div className="content my-content-addition">

                <h6>Start your development with no hassle!</h6>
                <p className="mb-3">Common problem of templates is that all code is deeply integrated into the core. This limits your freedom in decreasing amount of code, i.e. it becomes pretty difficult to remove unnecessary code from the project. Limitless allows you to remove unnecessary and extra code easily just by disabling styling of certain components in <code>_config.scss</code>. Styling of all 3rd party components are stored in separate SCSS files that begin with <code>$enable-[component]</code> condition, which checks if this component is enabled in SCSS configuration and either includes or excludes it from bundled CSS file. Use only components you actually need!</p>

                <h6>What is this?</h6>
                <p className="mb-3">Starter kit is a set of pages, useful for developers to start development process from scratch. Each layout includes base components only: layout, page kits, color system which is still optional, bootstrap files and bootstrap overrides. No extra CSS/JS files and markup. CSS files are compiled without any plugins or components. Starter kit is moved to a separate folder for better accessibility.</p>

                <h6>How does it work?</h6>
                <p>You open one of the starter pages, add necessary plugins, enable components in <code>_config.scss</code> file, compile new CSS. That's it. It's also recommended to open one of main pages with functionality you need and copy all paths/JS code from there to your new page, if you don't need to change file structure.</p>

                <h6>What is this?</h6>
                <p className="mb-3">Starter kit is a set of pages, useful for developers to start development process from scratch. Each layout includes base components only: layout, page kits, color system which is still optional, bootstrap files and bootstrap overrides. No extra CSS/JS files and markup. CSS files are compiled without any plugins or components. Starter kit is moved to a separate folder for better accessibility.</p>

                <h6>How does it work?</h6>
                <p>You open one of the starter pages, add necessary plugins, enable components in <code>_config.scss</code> file, compile new CSS. That's it. It's also recommended to open one of main pages with functionality you need and copy all paths/JS code from there to your new page, if you don't need to change file structure.</p>

            </div>
        </>
    );
}
