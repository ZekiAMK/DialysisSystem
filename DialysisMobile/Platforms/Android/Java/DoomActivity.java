package com.companyname.mobile;

import org.libsdl.app.SDLActivity;
import android.content.Intent;

public class DoomActivity extends SDLActivity {
    @Override
    protected String[] getArguments() {
        Intent intent = getIntent();
        String wadPath = intent.getStringExtra("wadPath");
        if (wadPath != null) {
            return new String[]{ wadPath };
        }
        return new String[0];
    }

    @Override
    protected String[] getLibraries() {
        return new String[]{
            "main"
        };
    }
}