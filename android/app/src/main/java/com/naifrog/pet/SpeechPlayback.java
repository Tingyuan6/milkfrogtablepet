package com.naifrog.pet;

import android.content.Context;
import android.content.res.AssetFileDescriptor;
import android.media.AudioAttributes;
import android.media.MediaPlayer;
import android.widget.Toast;

final class SpeechPlayback {
    private final Context context;
    private final String file;
    private MediaPlayer player;
    private boolean active;

    SpeechPlayback(Context context, String file) {
        this.context = context;
        this.file = file;
    }

    void play() {
        stop();
        try (AssetFileDescriptor fd = context.getAssets().openFd(file)) {
            player = new MediaPlayer();
            player.setAudioAttributes(
                    new AudioAttributes.Builder()
                            .setUsage(AudioAttributes.USAGE_MEDIA)
                            .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                            .build());
            player.setDataSource(fd.getFileDescriptor(), fd.getStartOffset(), fd.getLength());
            player.setOnCompletionListener(p -> active = false);
            player.setOnErrorListener(
                    (p, what, extra) -> {
                        active = false;
                        return true;
                    });
            player.prepare();
            player.start();
            active = true;
        } catch (Exception error) {
            stop();
            Toast.makeText(context, "暂时无法播放奶蛙语音", Toast.LENGTH_SHORT).show();
        }
    }

    boolean playing() {
        return active;
    }

    int position() {
        if (player == null || !active) return 0;
        try {
            return player.getCurrentPosition();
        } catch (IllegalStateException error) {
            active = false;
            return 0;
        }
    }

    void stop() {
        active = false;
        if (player != null) {
            player.release();
            player = null;
        }
    }
}
