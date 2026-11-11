#ifndef _XSTREAMBUF_H_
#define _XSTREAMBUF_H_

#pragma warning(disable : 4786)
#include <algorithm>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <string>
#include <type_traits>

namespace leaf
{
class xstreambuf
{
    void*        m_pBuffer;
    unsigned int m_offset;
    size_t       m_size, m_capacity;
    size_t       m_chunksize;

    void init();

public:
    enum XBUF_POS
    {
        XBUF_BEGIN,
        XBUF_END
    };

    xstreambuf();
    xstreambuf(unsigned int size);
    xstreambuf(const xstreambuf& xbuf);
    virtual ~xstreambuf();

    void        seek(unsigned int n, XBUF_POS pos = XBUF_BEGIN);
    xstreambuf& write(const void* src, unsigned int n);
    xstreambuf& read(void* dest, unsigned int n);

    void clear();
    void flush();

    const void* data() const;
    void*       get_writebuf() const; /* Don't use as possible */

    unsigned int get_curpos() const; /* Gets the current position */
    size_t       size() const;
    void         resize(size_t s);
    size_t       capacity() const;
    bool         empty() const;

    void   set_chunksize(size_t size);
    size_t get_chunksize();

    template <class T,
              std::enable_if_t<!std::is_pointer<std::decay_t<T>>::value &&
                                   !std::is_same<std::decay_t<T>, std::string>::value,
                               int> = 0>
    xstreambuf& operator<<(const T& data)
    {
        return write(static_cast<const void*>(&data), static_cast<unsigned int>(sizeof(data)));
    }

    xstreambuf& operator<<(char* sz) { return (*this) << static_cast<const char*>(sz); }
    xstreambuf& operator<<(const char* sz)
    {
        if (sz == nullptr)
        {
            const char empty = '\0';
            return write(&empty, 1);
        }
        const size_t byteCount = std::strlen(sz) + 1;
        if (byteCount > static_cast<size_t>((std::numeric_limits<unsigned int>::max)()))
        {
            throw std::length_error("xstreambuf string write exceeds maximum length");
        }
        return write(sz, static_cast<unsigned int>(byteCount));
    }
    xstreambuf& operator<<(const std::string& str)
    {
        const size_t byteCount = str.size() + 1;
        if (byteCount > static_cast<size_t>((std::numeric_limits<unsigned int>::max)()))
        {
            throw std::length_error("xstreambuf std::string write exceeds maximum length");
        }
        return write(str.c_str(), static_cast<unsigned int>(byteCount));
    }

    template <class T,
              std::enable_if_t<!std::is_array<T>::value && !std::is_pointer<std::decay_t<T>>::value &&
                                   !std::is_same<std::decay_t<T>, std::string>::value,
                               int> = 0>
    xstreambuf& operator>>(T& data)
    {
        return read(reinterpret_cast<char*>(&data), static_cast<unsigned int>(sizeof(data)));
    }

    template <size_t N> xstreambuf& operator>>(char (&sz)[N])
    {
        static_assert(N > 0, "Destination char array must be non-empty");
        if (m_pBuffer == nullptr || m_offset >= m_size)
        {
            sz[0] = '\0';
            return *this;
        }

        const char*  start     = static_cast<const char*>(m_pBuffer) + m_offset;
        const size_t available = m_size - m_offset;
        const void*  nulPos    = std::memchr(start, '\0', available);
        const size_t strLen    = nulPos ? static_cast<size_t>(static_cast<const char*>(nulPos) - start) : available;
        const size_t copyLen   = std::min<size_t>(strLen, N - 1);
        if (copyLen > 0)
        {
            std::memcpy(sz, start, copyLen);
        }
        sz[copyLen] = '\0';

        const size_t consumed = nulPos ? (strLen + 1) : available;
        m_offset              = static_cast<unsigned int>(m_offset + consumed);
        return *this;
    }

    template <typename TCharPtr,
              std::enable_if_t<!std::is_array<std::remove_reference_t<TCharPtr>>::value &&
                                   std::is_same<std::decay_t<TCharPtr>, char*>::value,
                               int> = 0>
    xstreambuf& operator>>(TCharPtr&& sz)
    {
        if (sz == nullptr)
        {
            return *this;
        }
        if (m_pBuffer == nullptr || m_offset >= m_size)
        {
            sz[0] = '\0';
            return *this;
        }

        const char*  start     = static_cast<const char*>(m_pBuffer) + m_offset;
        const size_t available = m_size - m_offset;
        const void*  nulPos    = std::memchr(start, '\0', available);
        const size_t strLen    = nulPos ? static_cast<size_t>(static_cast<const char*>(nulPos) - start) : available;
        if (strLen > 0)
        {
            std::memcpy(sz, start, strLen);
        }
        sz[strLen] = '\0';

        const size_t consumed = nulPos ? (strLen + 1) : available;
        m_offset              = static_cast<unsigned int>(m_offset + consumed);
        return *this;
    }

    xstreambuf& operator>>(std::string& str)
    {
        if (m_pBuffer == nullptr || m_offset >= m_size)
        {
            str.clear();
            return *this;
        }

        const char*  start     = static_cast<const char*>(m_pBuffer) + m_offset;
        const size_t available = m_size - m_offset;
        const void*  nulPos    = std::memchr(start, '\0', available);
        const size_t strLen    = nulPos ? static_cast<size_t>(static_cast<const char*>(nulPos) - start) : available;
        str.assign(start, strLen);

        const size_t consumed = nulPos ? (strLen + 1) : available;
        m_offset              = static_cast<unsigned int>(m_offset + consumed);
        return *this;
    }

    xstreambuf& operator=(const xstreambuf& buf)
    {
        if (this == &buf)
        {
            return *this;
        }

        const size_t newSize = buf.size();
        if (m_capacity < newSize)
        {
            resize(newSize);
        }

        if (newSize > 0 && m_pBuffer != nullptr && buf.data() != nullptr)
        {
            std::memcpy(m_pBuffer, buf.data(), newSize);
        }

        m_size   = newSize;
        m_offset = static_cast<unsigned int>(newSize);
        return *this;
    }
};

}

#endif  // _XSTREAMBUF_H_