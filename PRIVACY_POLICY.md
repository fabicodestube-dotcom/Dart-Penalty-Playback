# Privacy Policy for Dart Penalty Playback

The protection of your personal data is of utmost importance to us. This application is programmed to respect the user's privacy to the maximum extent. Since the app operates entirely without a server connection, no personal data is transmitted to us or any third parties.

## 1. General Information on Data Collection
This application does not collect, store, use, or transmit any personal data over the internet. 

* **No Registration:** Using the app does not require a user account, login, or registration.
* **No Analytics:** We do not use any tracking tools, analytics software (such as Unity Analytics), or crash-reporting services that log your usage behavior.
* **No Advertising:** The app does not contain any advertising networks or ad APIs.
* **No Cloud Connectivity:** There are no interfaces to cloud storage or external databases.

## 2. Device Access, Permissions, and Local Data Processing (Dart Game Statistics & Recorder)
For its core features – tracking your dart games and using the audio recorder – the application processes data and requires hardware access. Everything is strictly limited to your local device.

* **Dart Game Statistics:** The app tracks and processes gameplay statistics (e.g., scores, match history, averages, and game results). These statistics are stored exclusively on your device in a local database file using the JSON format. This file is used solely to display your historical performance within the app.
* **Microphone Permission:** This permission is requested at runtime as soon as you start the recorder for the first time. This access is used exclusively to create the audio recordings you request.
* **Local Storage Structure (WAV & JSON):** The created audio recordings are saved as WAV files within the secure, internal app directory (`Application.persistentDataPath`). For organization, these files are structured within a subfolder named "recordings" and further divided into category-based subfolders. The assignment of these WAV files is managed by a separate local JSON index file.
* **No Transmission:** Neither your dart game statistics, the audio recordings, nor any JSON database files are ever sent to a server, analyzed by us, or made accessible to third parties. 
* **Automatic Deletion Upon Uninstallation:** Since all databases, statistics, WAV files, and JSON files are stored in the dedicated app folder secured by the operating system, all application data is automatically and completely deleted from the device by the Android operating system when the app is uninstalled.

## 3. Data Processing via Third-Party Platforms (F-Droid)
This application does not collect download data, as the developer does not host a private web server. Instead, the installation file (APK) is distributed via third-party repositories and platforms, primarily F-Droid. 

Please note that when you download or update the app through F-Droid or another platform, that specific platform may independently log technical data (such as your IP address or the time of download). The developer of this app has no control over, or access to, the server logs of these third-party platforms. For details on how your download data is handled, please consult the privacy policy of F-Droid or the respective platform you use.

## 4. Contact and Imprint
If you have any questions regarding this Privacy Policy or the app, you can contact us at the following address:

Fabis Codestube
fabi.codestube@gmail.com

You can find the source code or updates for this project on our GitHub page:
https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/

Date of Last Update: [Current Date]
