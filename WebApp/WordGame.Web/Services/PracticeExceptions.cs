namespace WordGame.Web.Services;

public abstract class PracticeException(string message) : Exception(message);

public sealed class PracticeValidationException(string message)
    : PracticeException(message);

public sealed class PracticeSessionNotFoundException()
    : PracticeException("練習已結束或逾時，請重新開始。");

public sealed class PracticeStateException(string message)
    : PracticeException(message);
