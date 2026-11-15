using System.Diagnostics;
using System.Text;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Microsoft.Maui.Storage;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace MyExcelMAUIApp.Services;

public sealed class GoogleDriveService : IDisposable
{
    private const string AppDataFolderName = "appDataFolder";
    private const string ClientSecretsFileName = "client_secrets.json";
    private const string ContentMimeType = "application/json";
    private const string UserId = "user";
    private const string ApplicationName = "MyExcel";

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly string _tokenStoreDirectory = Path.Combine(FileSystem.AppDataDirectory, "GoogleAuth");

    private UserCredential? _credential;
    private DriveService? _driveService;

    public Task<IReadOnlyList<DriveFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<DriveFileInfo>>(async service =>
        {
            var request = service.Files.List();
            request.Spaces = AppDataFolderName;
            request.Fields = "files(id, name)";
            request.OrderBy = "modifiedTime desc";
            request.PageSize = 100;

            var result = await request.ExecuteAsync(cancellationToken);
            return (result.Files ?? [])
                .Select(file => new DriveFileInfo(file.Id, file.Name))
                .ToList();
        });

    public Task UploadAsync(string fileName, string content, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async service =>
        {
            var existingId = await FindFileIdAsync(service, fileName, cancellationToken);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            IUploadProgress progress;
            if (existingId is null)
            {
                var metadata = new DriveFile
                {
                    Name = fileName,
                    Parents = [AppDataFolderName],
                };
                var createRequest = service.Files.Create(metadata, stream, ContentMimeType);
                createRequest.Fields = "id";
                progress = await createRequest.UploadAsync(cancellationToken);
            }
            else
            {
                var updateRequest = service.Files.Update(new DriveFile(), existingId, stream, ContentMimeType);
                progress = await updateRequest.UploadAsync(cancellationToken);
            }

            if (progress.Status != UploadStatus.Completed)
            {
                throw new GoogleDriveException(
                    $"Не вдалося вивантажити файл: {progress.Exception?.Message ?? "невідома помилка"}",
                    progress.Exception);
            }
        });

    public Task<string> DownloadAsync(string fileId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async service =>
        {
            using var stream = new MemoryStream();
            var progress = await service.Files.Get(fileId).DownloadAsync(stream, cancellationToken);
            if (progress.Status != DownloadStatus.Completed)
            {
                throw new GoogleDriveException(
                    $"Не вдалося завантажити файл: {progress.Exception?.Message ?? "невідома помилка"}",
                    progress.Exception);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        });

    public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(service => service.Files.Delete(fileId).ExecuteAsync(cancellationToken));

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            await TryRevokeTokenAsync(cancellationToken);
            await new FileDataStore(_tokenStoreDirectory, fullPath: true).ClearAsync();
            _driveService?.Dispose();
            _driveService = null;
            _credential = null;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public void Dispose()
    {
        _driveService?.Dispose();
        _initializationLock.Dispose();
    }

    private Task ExecuteAsync(Func<DriveService, Task> operation) =>
        ExecuteAsync(async service =>
        {
            await operation(service);
            return true;
        });

    private async Task<T> ExecuteAsync<T>(Func<DriveService, Task<T>> operation)
    {
        try
        {
            var service = await GetServiceAsync();
            return await operation(service);
        }
        catch (GoogleDriveException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new GoogleDriveException("Операцію з Google Drive скасовано.", exception);
        }
        catch (TokenResponseException exception)
        {
            throw new GoogleDriveException(
                "Помилка авторизації Google. Спробуйте вийти з облікового запису та увійти знову.", exception);
        }
        catch (GoogleApiException exception)
        {
            throw new GoogleDriveException($"Google Drive повернув помилку: {exception.Message}", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new GoogleDriveException("Немає з'єднання з Google Drive. Перевірте підключення до Інтернету.", exception);
        }
    }

    private async Task<DriveService> GetServiceAsync()
    {
        if (_driveService is not null)
        {
            return _driveService;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_driveService is not null)
            {
                return _driveService;
            }

            var clientSecrets = await LoadClientSecretsAsync();

            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                [DriveService.Scope.DriveAppdata],
                UserId,
                CancellationToken.None,
                new FileDataStore(_tokenStoreDirectory, fullPath: true));

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName,
            });
            return _driveService;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task TryRevokeTokenAsync(CancellationToken cancellationToken)
    {
        if (_credential is null)
        {
            return;
        }

        try
        {
            await _credential.RevokeTokenAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is TokenResponseException or HttpRequestException)
        {
            Debug.WriteLine($"Не вдалося відкликати токен Google: {exception.Message}");
        }
    }

    private static async Task<ClientSecrets> LoadClientSecretsAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(ClientSecretsFileName);
            return (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new GoogleDriveException(
                "Не знайдено файл client_secrets.json. Покладіть його в папку Resources/Raw " +
                "та перезберіть проєкт (див. README, розділ «Налаштування Google Drive»).",
                exception);
        }
    }

    private static async Task<string?> FindFileIdAsync(
        DriveService service, string fileName, CancellationToken cancellationToken)
    {
        var request = service.Files.List();
        request.Spaces = AppDataFolderName;
        request.Fields = "files(id)";
        request.Q = $"name = '{EscapeQueryValue(fileName)}' and trashed = false";
        var result = await request.ExecuteAsync(cancellationToken);
        return result.Files?.FirstOrDefault()?.Id;
    }

    private static string EscapeQueryValue(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");
}
