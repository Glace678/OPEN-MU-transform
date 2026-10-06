package net.munique.openmu.gm;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import java.io.ByteArrayInputStream;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

/**
 * XC-21: host-side coverage of the GM key-protection path and the pure helpers
 * of {@link MobileGmApiClient}. The constructor's transport guard is the A-04
 * boundary that keeps the shared write key off cleartext public hosts; the JSON
 * parsing helpers are private static, so they are driven through reflection
 * without touching a network socket.
 */
public final class MobileGmApiClientTest {

    private static MobileGmApiClient newClient(String baseUrl) throws MobileGmApiClient.ApiException {
        return new MobileGmApiClient(baseUrl, "test-key");
    }

    private static Object invokeStatic(String name, Class<?>[] types, Object... args) throws Exception {
        Method method = MobileGmApiClient.class.getDeclaredMethod(name, types);
        method.setAccessible(true);
        return method.invoke(null, args);
    }

    // --- A-04 constructor transport guard -----------------------------------

    @Test
    public void acceptsHttpsToAnyHost() throws Exception {
        newClient("https://gm.example.com");
        newClient("https://gm.example.com:8443/");
    }

    @Test
    public void acceptsCleartextOnlyForLocalHosts() throws Exception {
        for (String url : new String[]{
            "http://localhost:5080",
            "http://127.0.0.1:5080",
            "http://127.1.2.3:5080",
            "http://10.0.2.2:5080",
            "http://192.168.1.10:5080",
            "http://172.16.0.1:5080",
            "http://172.31.255.255:5080",
            "http://169.254.9.9:5080",
        }) {
            newClient(url);
        }
    }

    @Test
    public void rejectsCleartextToPublicHosts() {
        for (String url : new String[]{
            "http://8.8.8.8:5080",
            "http://172.32.0.1:5080",
            "http://172.15.0.1:5080",
            "http://example.com:5080",
        }) {
            MobileGmApiClient.ApiException error =
                assertThrows(url, MobileGmApiClient.ApiException.class, () -> newClient(url));
            assertTrue(error.getMessage(), error.getMessage().contains("明文 HTTP"));
        }
    }

    @Test
    public void rejectsUnsupportedProtocolAndMalformedUrl() {
        assertThrows(MobileGmApiClient.ApiException.class,
            () -> newClient("ftp://192.168.1.2:5080"));
        assertThrows(MobileGmApiClient.ApiException.class,
            () -> newClient("not a url at all"));
    }

    @Test
    public void trailingSlashesAreTrimmedBeforeRequests() throws Exception {
        MobileGmApiClient client = newClient("https://gm.example.com///");
        java.lang.reflect.Field field = MobileGmApiClient.class.getDeclaredField("baseUrl");
        field.setAccessible(true);
        assertEquals("https://gm.example.com", field.get(client));
    }

    // --- parseExcellentNumbers ----------------------------------------------

    @Test
    public void excellentNumbersFallbackSynthesizesOneToCount() throws Exception {
        JSONObject item = new JSONObject();
        int[] result = (int[]) invokeStatic("parseExcellentNumbers",
            new Class<?>[]{JSONObject.class, int.class}, item, 3);
        assertArrayEquals(new int[]{1, 2, 3}, result);
    }

    @Test
    public void excellentNumbersAreParsedSparseAndRangeFiltered() throws Exception {
        JSONObject item = new JSONObject().put("excellentOptionNumbers",
            new JSONArray(Arrays.asList(-5, 0, 2, 7, 32, 40, 2)));
        int[] result = (int[]) invokeStatic("parseExcellentNumbers",
            new Class<?>[]{JSONObject.class, int.class}, item, 3);
        // Out-of-range values are dropped; duplicates are kept (the parser does
        // not dedupe - mask assembly never sees one bit twice anyway).
        assertArrayEquals(new int[]{2, 7, 2}, result);
    }

    // --- extractMessage -----------------------------------------------------

    @Test
    public void messageExtractedFromMessageOrErrorField() throws Exception {
        assertEquals("denied", invokeStatic("extractMessage",
            new Class<?>[]{String.class}, "{\"message\":\"denied\"}"));
        assertEquals("bad", invokeStatic("extractMessage",
            new Class<?>[]{String.class}, "{\"error\":\"bad\"}"));
        assertEquals("", invokeStatic("extractMessage",
            new Class<?>[]{String.class}, "plain text error body"));
        assertEquals("", invokeStatic("extractMessage",
            new Class<?>[]{String.class}, "   "));
    }

    // --- readResponse: response size cap ------------------------------------

    @Test
    public void readResponseRejectsOversizedPayload() throws Exception {
        byte[] payload = new byte[1024 * 1024 + 1];
        Arrays.fill(payload, (byte) 'a');
        ByteArrayInputStream stream = new ByteArrayInputStream(payload);

        Exception thrown = assertThrows(Exception.class, () -> invokeStatic("readResponse",
            new Class<?>[]{java.io.InputStream.class}, stream));
        assertTrue(thrown.getCause() instanceof MobileGmApiClient.ApiException);
    }

    @Test
    public void readResponseAcceptsPayloadAtTheLimit() throws Exception {
        byte[] payload = new byte[1024 * 1024];
        Arrays.fill(payload, (byte) 'a');
        String result = (String) invokeStatic("readResponse",
            new Class<?>[]{java.io.InputStream.class},
            new ByteArrayInputStream(payload));
        assertEquals(payload.length, result.length());
    }

    @Test
    public void readResponseHandlesNullStream() throws Exception {
        assertEquals("", invokeStatic("readResponse",
            new Class<?>[]{java.io.InputStream.class}, new Object[]{null}));
    }

    // --- trimTrailingSlash / cleanString ------------------------------------

    @Test
    public void trimTrailingSlashHandlesNullAndMultipleSlashes() throws Exception {
        assertEquals("", invokeStatic("trimTrailingSlash",
            new Class<?>[]{String.class}, new Object[]{null}));
        assertEquals("https://x", invokeStatic("trimTrailingSlash",
            new Class<?>[]{String.class}, "  https://x///  "));
    }

    @Test
    public void cleanStringDefaultsNullToEmpty() throws Exception {
        assertEquals("", invokeStatic("cleanString",
            new Class<?>[]{String.class}, new Object[]{null}));
        assertEquals("v", invokeStatic("cleanString",
            new Class<?>[]{String.class}, "  v\t "));
    }
}
