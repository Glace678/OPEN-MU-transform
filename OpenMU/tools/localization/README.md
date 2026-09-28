# Mainland Chinese resources

The OpenMU admin panel keeps Simplified Chinese as the exact `zh-CN` BCP-47
culture. Do not shorten it to `zh`; `zh-CN` and `zh-TW` must remain distinct in
cookies, selectors, resource lookup, and serialized localized values.

Run these commands from the repository root after changing an English source
resource or a reviewed translation:

```powershell
tools/localization/Generate-ZhCnResources.ps1
tools/localization/Test-ZhCnResources.ps1
```

`zh-cn-translations.json` contains direct translations for the admin panel,
shared controls, map view, and system settings. Repeated model captions are
reviewed once in `model-caption-translations.json` and applied to every matching
model resource key. Missing values, unknown keys, placeholder changes, empty
translations, mojibake, and reviewed Taiwan-only terms fail generation or the
audit.
