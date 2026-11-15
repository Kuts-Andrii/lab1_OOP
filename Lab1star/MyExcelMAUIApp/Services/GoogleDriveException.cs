namespace MyExcelMAUIApp.Services;

public sealed class GoogleDriveException(string message, Exception? innerException = null)
    : Exception(message, innerException);
