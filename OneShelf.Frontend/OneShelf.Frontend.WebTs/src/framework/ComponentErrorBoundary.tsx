// An exception thrown while rendering, or in an effect, of a component below: logged as Blazor's renderer logs an
// unhandled exception in a component (handleRendererException), which shows the error bar. Blazor leaves the page as it
// was; React has already removed what is below this boundary, so nothing is shown in its place (PORTING.md,
// Departures) until the next navigation, which renders it anew.

import { Component, type ReactNode } from 'react';
import type { Location } from 'react-router-dom';
import { handleRendererException } from './unhandledExceptions';

interface ComponentErrorBoundaryProps {
    /** The router's location: a new one (a navigation) renders the children anew. */
    location: Location;
    children: ReactNode;
}

interface ComponentErrorBoundaryState {
    failed: boolean;
    location: Location;
}

export class ComponentErrorBoundary extends Component<ComponentErrorBoundaryProps, ComponentErrorBoundaryState> {
    override state: ComponentErrorBoundaryState = { failed: false, location: this.props.location };

    static getDerivedStateFromProps(props: ComponentErrorBoundaryProps, state: ComponentErrorBoundaryState): Partial<ComponentErrorBoundaryState> | null {
        return props.location !== state.location ? { failed: false, location: props.location } : null;
    }

    static getDerivedStateFromError(): Partial<ComponentErrorBoundaryState> {
        return { failed: true };
    }

    override componentDidCatch(error: unknown): void {
        handleRendererException(error);
    }

    override render(): ReactNode {
        return this.state.failed ? null : this.props.children;
    }
}
