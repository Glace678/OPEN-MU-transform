# Empty on purpose.
#
# The previous content kept @android.webkit.JavascriptInterface methods, but the
# GM app has no WebView and no @JavascriptInterface annotation anywhere in its
# sources, so the rule matched nothing. It was removed rather than left as dead
# configuration: a keep rule that protects a nonexistent annotation silently
# trains readers to ignore this file. If a WebView is reintroduced, re-add the
# rule alongside the annotation.
