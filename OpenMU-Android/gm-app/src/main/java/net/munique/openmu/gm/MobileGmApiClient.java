package net.munique.openmu.gm;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONTokener;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class MobileGmApiClient {
    private static final int CONNECT_TIMEOUT_MS = 7000;
    private static final int READ_TIMEOUT_MS = 10000;
    private static final int MAX_RESPONSE_CHARS = 1024 * 1024;

    private final String baseUrl;
    private final String packageKey;

    MobileGmApiClient(String baseUrl, String packageKey) {
        this.baseUrl = trimTrailingSlash(baseUrl);
        this.packageKey = packageKey;
    }

    Status loadStatus() throws ApiException {
        Object root = request("GET", "/status", null);
        if (!(root instanceof JSONObject)) {
            throw new ApiException("服务器状态响应不是 JSON 对象");
        }
        JSONObject object = (JSONObject) root;
        String accountName = cleanString(object.optString("accountName", ""));
        if (accountName.isEmpty()) {
            Object localAccount = object.opt("localAccount");
            if (localAccount instanceof JSONObject) {
                JSONObject account = (JSONObject) localAccount;
                accountName = cleanString(account.optString("loginName", account.optString("name", "")));
            } else if (localAccount instanceof String) {
                accountName = cleanString((String) localAccount);
            }
        }
        if (accountName.isEmpty()) {
            accountName = cleanString(object.optString("account", ""));
        }
        if (accountName.isEmpty()) {
            accountName = cleanString(object.optString("loginName", ""));
        }

        JSONArray array = object.optJSONArray("characters");
        if (array == null) {
            array = object.optJSONArray("onlineCharacters");
        }
        if (array == null) {
            throw new ApiException("服务器状态响应缺少 onlineCharacters");
        }
        List<CharacterOption> characters = new ArrayList<>();
        for (int index = 0; index < array.length(); index++) {
            JSONObject character = array.optJSONObject(index);
            if (character == null || !character.optBoolean("online", true)) {
                continue;
            }
            String id = cleanString(character.optString("characterId", character.optString("id", "")));
            String name = cleanString(character.optString("name",
                character.optString("characterName", "")));
            long money = character.optLong("money", -1L);
            int level = character.optInt("level", 0);
            if (!id.isEmpty() && !name.isEmpty()) {
                characters.add(new CharacterOption(id, name, level, money));
            }
        }
        return new Status(accountName, characters);
    }

    List<ItemOption> searchItems(String query) throws ApiException {
        String encoded = android.net.Uri.encode(query == null ? "" : query.trim());
        Object root = request("GET", "/items?q=" + encoded, null);
        JSONArray array;
        if (root instanceof JSONArray) {
            array = (JSONArray) root;
        } else if (root instanceof JSONObject) {
            array = ((JSONObject) root).optJSONArray("items");
        } else {
            array = null;
        }
        if (array == null) {
            throw new ApiException("物品搜索响应缺少 items 数组");
        }
        List<ItemOption> items = new ArrayList<>();
        for (int index = 0; index < array.length(); index++) {
            JSONObject item = array.optJSONObject(index);
            if (item == null || !item.has("group") || !item.has("number")) {
                continue;
            }
            try {
                int group = item.getInt("group");
                int number = item.getInt("number");
                String name = cleanString(item.optString("name", item.optString("displayName", "")));
                int maxLevel = Math.max(0, Math.min(255,
                    item.optInt("maxLevel", item.optInt("maximumLevel", 15))));
                boolean canHaveSkill = item.optBoolean("hasSkill", item.optBoolean("canHaveSkill", true));
                boolean canHaveLuck = item.optBoolean("canHaveLuck", false);
                boolean canHaveAdditional = item.optBoolean("canHaveAdditionalOption",
                    item.optBoolean("canHaveAdditional", false));
                int excellentCount = Math.max(0, Math.min(31,
                    item.optInt("excellentOptionCount", item.optInt("excellentCount", 0))));
                if (!name.isEmpty()) {
                    items.add(new ItemOption(group, number, name, maxLevel, canHaveSkill,
                        canHaveLuck, canHaveAdditional, excellentCount));
                }
            } catch (JSONException ignored) {
                // Skip one malformed search result without hiding valid results.
            }
        }
        return items;
    }

    GrantResult grantItem(GrantRequest request) throws ApiException {
        JSONObject body = new JSONObject();
        try {
            body.put("requestId", request.requestId);
            body.put("characterId", request.characterId);
            body.put("group", request.group);
            body.put("number", request.number);
            body.put("level", request.level);
            body.put("quantity", request.quantity);
            body.put("hasSkill", request.hasSkill);
            body.put("hasLuck", request.hasLuck);
            body.put("additionalOptionLevel", request.additionalOptionLevel);
            body.put("excellentMask", request.excellentMask);
        } catch (JSONException error) {
            throw new ApiException("无法生成发放请求", error);
        }
        Object root = request("POST", "/grant-item", body.toString());
        String message = "物品已成功发放";
        if (root instanceof JSONObject) {
            JSONObject object = (JSONObject) root;
            String returned = cleanString(object.optString("message", ""));
            if (object.has("success") && !object.optBoolean("success", false)) {
                throw new ApiException(returned.isEmpty() ? "服务器拒绝发放物品" : returned);
            }
            if (!returned.isEmpty()) {
                message = returned;
            }
        }
        return new GrantResult(message);
    }

    GrantResult grantZen(ZenRequest request) throws ApiException {
        JSONObject body = new JSONObject();
        try {
            body.put("requestId", request.requestId);
            body.put("characterId", request.characterId);
            body.put("amount", request.amount);
        } catch (JSONException error) {
            throw new ApiException("无法生成金币发放请求", error);
        }
        Object root = request("POST", "/grant-zen", body.toString());
        String message = "金币已成功发放";
        if (root instanceof JSONObject) {
            JSONObject object = (JSONObject) root;
            String returned = cleanString(object.optString("message", ""));
            if (object.has("success") && !object.optBoolean("success", false)) {
                throw new ApiException(returned.isEmpty() ? "服务器拒绝发放金币" : returned);
            }
            if (!returned.isEmpty()) {
                message = returned;
            }
        }
        return new GrantResult(message);
    }

    private Object request(String method, String path, String body) throws ApiException {
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(baseUrl + "/api/mobile-gm" + path).openConnection();
            connection.setRequestMethod(method);
            connection.setConnectTimeout(CONNECT_TIMEOUT_MS);
            connection.setReadTimeout(READ_TIMEOUT_MS);
            connection.setRequestProperty("Accept", "application/json");
            connection.setRequestProperty("X-OpenMU-Mobile-Key", packageKey);
            connection.setUseCaches(false);
            if (body != null) {
                byte[] bytes = body.getBytes(StandardCharsets.UTF_8);
                connection.setDoOutput(true);
                connection.setFixedLengthStreamingMode(bytes.length);
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                try (OutputStream output = connection.getOutputStream()) {
                    output.write(bytes);
                }
            }

            int statusCode = connection.getResponseCode();
            InputStream stream = statusCode >= 200 && statusCode < 300
                ? connection.getInputStream() : connection.getErrorStream();
            String response = readResponse(stream);
            if (statusCode < 200 || statusCode >= 300) {
                String serverMessage = extractMessage(response);
                throw new ApiException("服务器返回 HTTP " + statusCode
                    + (serverMessage.isEmpty() ? "" : "：" + serverMessage));
            }
            if (response.trim().isEmpty()) {
                return new JSONObject();
            }
            try {
                return new JSONTokener(response).nextValue();
            } catch (JSONException error) {
                throw new ApiException("服务器返回了无效 JSON", error);
            }
        } catch (SocketTimeoutException error) {
            throw new ApiException("连接服务器超时，请检查地址和局域网", error);
        } catch (IOException error) {
            throw new ApiException("无法连接服务器：" + cleanString(error.getMessage()), error);
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private static String readResponse(InputStream stream) throws IOException, ApiException {
        if (stream == null) {
            return "";
        }
        StringBuilder response = new StringBuilder();
        char[] buffer = new char[4096];
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8))) {
            int count;
            while ((count = reader.read(buffer)) >= 0) {
                if (response.length() + count > MAX_RESPONSE_CHARS) {
                    throw new ApiException("服务器响应过大");
                }
                response.append(buffer, 0, count);
            }
        }
        return response.toString();
    }

    private static String extractMessage(String response) {
        if (response == null || response.trim().isEmpty()) {
            return "";
        }
        try {
            Object parsed = new JSONTokener(response).nextValue();
            if (parsed instanceof JSONObject) {
                JSONObject object = (JSONObject) parsed;
                return cleanString(object.optString("message", object.optString("error", "")));
            }
        } catch (JSONException ignored) {
            // A non-JSON error body is intentionally not shown to avoid rendering server internals.
        }
        return "";
    }

    private static String trimTrailingSlash(String value) {
        String result = value == null ? "" : value.trim();
        while (result.endsWith("/")) {
            result = result.substring(0, result.length() - 1);
        }
        return result;
    }

    private static String cleanString(String value) {
        return value == null ? "" : value.trim();
    }

    static final class Status {
        final String accountName;
        final List<CharacterOption> characters;

        Status(String accountName, List<CharacterOption> characters) {
            this.accountName = accountName;
            this.characters = Collections.unmodifiableList(new ArrayList<>(characters));
        }
    }

    static final class CharacterOption {
        final String id;
        final String name;
        final int level;
        final long money;

        CharacterOption(String id, String name, int level, long money) {
            this.id = id;
            this.name = name;
            this.level = level;
            this.money = money;
        }

        @Override
        public String toString() {
            return name + (level > 0 ? "  (Lv " + level + ")" : "");
        }
    }

    static final class ItemOption {
        final int group;
        final int number;
        final String name;
        final int maxLevel;
        final boolean canHaveSkill;
        final boolean canHaveLuck;
        final boolean canHaveAdditional;
        final int excellentCount;

        ItemOption(int group, int number, String name, int maxLevel, boolean canHaveSkill,
                   boolean canHaveLuck, boolean canHaveAdditional, int excellentCount) {
            this.group = group;
            this.number = number;
            this.name = name;
            this.maxLevel = maxLevel;
            this.canHaveSkill = canHaveSkill;
            this.canHaveLuck = canHaveLuck;
            this.canHaveAdditional = canHaveAdditional;
            this.excellentCount = excellentCount;
        }

        @Override
        public String toString() {
            return name + "  (" + group + ":" + number + ")";
        }
    }

    static final class GrantRequest {
        final String requestId;
        final String characterId;
        final int group;
        final int number;
        final int level;
        final int quantity;
        final boolean hasSkill;
        final boolean hasLuck;
        final int additionalOptionLevel;
        final int excellentMask;

        GrantRequest(String requestId, String characterId, int group, int number,
                     int level, int quantity, boolean hasSkill, boolean hasLuck,
                     int additionalOptionLevel, int excellentMask) {
            this.requestId = requestId;
            this.characterId = characterId;
            this.group = group;
            this.number = number;
            this.level = level;
            this.quantity = quantity;
            this.hasSkill = hasSkill;
            this.hasLuck = hasLuck;
            this.additionalOptionLevel = additionalOptionLevel;
            this.excellentMask = excellentMask;
        }
    }

    static final class GrantResult {
        final String message;

        GrantResult(String message) {
            this.message = message;
        }
    }

    static final class ZenRequest {
        final String requestId;
        final String characterId;
        final long amount;

        ZenRequest(String requestId, String characterId, long amount) {
            this.requestId = requestId;
            this.characterId = characterId;
            this.amount = amount;
        }
    }

    static final class ApiException extends Exception {
        ApiException(String message) {
            super(message);
        }

        ApiException(String message, Throwable cause) {
            super(message, cause);
        }
    }
}
