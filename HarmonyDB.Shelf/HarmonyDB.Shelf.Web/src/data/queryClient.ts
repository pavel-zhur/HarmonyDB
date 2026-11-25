import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';

export const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            // an answer of the server is final; a lost connection, or a server away for a moment, is tried again
            retry: (count, error) => error instanceof ApiError && (error.code === 'network' || error.status >= 500) && count < 3,
        },
    },
});
