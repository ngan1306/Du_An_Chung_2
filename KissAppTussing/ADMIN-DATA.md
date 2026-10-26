# Admin dashboard and data export

The application saves account records and activity in the SQL Server configured by `ConnectionStrings:DatingApp`. The owner dashboard shows totals and the ten newest accounts. Its JSON export includes profiles, interests, profile photo URLs, swipes, matches, journey posts, likes, and comments. It deliberately omits password hashes, private chat contents, and attachment files. Chat messages and attachments are still stored on the server to provide chat; the dashboard only shows a message count. Photo and post media are represented by their URLs, not file bytes.

## Grant access to the owner account

1. Create your account in the app and sign in.
2. While signed in, open `GET /api/profiles/me` and copy the `id` value.
3. Configure that account ID as `Admin:UserIds`, then restart the application. Only configured account IDs can open the dashboard or call its API.

For a local PowerShell session:

```powershell
$env:Admin__UserIds__0 = "YOUR-ACCOUNT-ID"
dotnet run
```

For a persistent setup, add the ID to the `Admin:UserIds` array in the app's configuration or set the matching environment variable on the host. Do not put a real user's ID in a checked-in configuration file.

The registration screen discloses server-side storage and owner access to the exportable records. The export endpoint also records the requesting owner account ID in the server log; it does not log exported content.
