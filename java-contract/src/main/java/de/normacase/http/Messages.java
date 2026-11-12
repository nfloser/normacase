package de.normacase.http;

import java.util.Locale;
import java.util.ResourceBundle;

final class Messages {
    private static final ResourceBundle TEXT = ResourceBundle.getBundle("messages", Locale.ROOT);
    static final String UNAUTHORIZED = TEXT.getString("unauthorized");
    static final String ERROR = TEXT.getString("error");
    private Messages() { }
}
