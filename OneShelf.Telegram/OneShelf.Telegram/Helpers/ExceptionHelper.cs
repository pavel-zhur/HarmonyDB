namespace OneShelf.Telegram.Helpers
{
    public static class ExceptionHelper
    {
        public static bool HasInside<T>(this Exception exception) where T : Exception
        {
            switch (exception)
            {
                case null:
                    return false;
                case T:
                    return true;
                case AggregateException aggregateException:
                    return aggregateException.InnerExceptions.Any(HasInside<T>);
                default:
                    return exception.InnerException?.HasInside<T>() ?? false;
            }
        }
    }
}
