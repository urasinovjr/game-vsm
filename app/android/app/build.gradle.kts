import java.util.Properties

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

val releaseKeys = Properties()
val releaseKeyFile = rootProject.file("key.properties")
if (releaseKeyFile.exists()) {
    releaseKeyFile.inputStream().use(releaseKeys::load)
}

android {
    namespace = "ru.gamevsm.conductor"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        applicationId = "ru.gamevsm.conductor"
        minSdk = 26
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        ndk { abiFilters += "arm64-v8a" }
    }

    if (releaseKeyFile.exists()) {
        signingConfigs {
            create("release") {
                storeFile = file(releaseKeys.getProperty("storeFile"))
                storePassword = releaseKeys.getProperty("storePassword")
                keyAlias = releaseKeys.getProperty("keyAlias")
                keyPassword = releaseKeys.getProperty("keyPassword")
            }
        }
        buildTypes.getByName("release") {
            signingConfig = signingConfigs.getByName("release")
            proguardFiles("proguard-rules.pro")
        }
    }

    androidResources {
        noCompress += listOf(".unity3d", ".ress", ".resource", ".obb", ".bundle", ".unityexp")
    }

    packaging {
        jniLibs.useLegacyPackaging = true
        jniLibs.excludes += setOf("lib/armeabi-v7a/**", "lib/x86_64/**")
    }

}

dependencies {
    implementation(files("libs/unityLibrary-release.aar"))
}

flutter {
    source = "../.."
}
