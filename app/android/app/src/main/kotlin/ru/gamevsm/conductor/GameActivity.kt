package ru.gamevsm.conductor

import android.content.Intent
import com.unity3d.player.UnityPlayerActivity

class GameActivity : UnityPlayerActivity() {
    fun returnToApp() {
        runOnUiThread {
            startActivity(Intent(this, MainActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            })
            finish()
        }
    }

    override fun onResume() {
        super.onResume()
        GameBridge.resume(this)
    }

    override fun onPause() {
        GameBridge.pause(this)
        super.onPause()
    }

    override fun onUnityPlayerUnloaded() {
        finish()
    }

    override fun onUnityPlayerQuitted() {
        finish()
    }
}
