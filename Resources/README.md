# Google Authentication Setup

To enable Google authentication in School Organizer, you need to:

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google Classroom API and Google Drive API
4. Create OAuth 2.0 credentials (Desktop application)
5. Download the credentials JSON file
6. Rename it to `credentials.json` and place it in this Resources folder

The credentials.json file should look like this:

```json
{
  "installed": {
    "client_id": "your-client-id.apps.googleusercontent.com",
    "project_id": "your-project-id",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
    "client_secret": "your-client-secret",
    "redirect_uris": ["http://localhost"]
  }
}
```

**Important**: Never commit the actual credentials.json file to version control!
