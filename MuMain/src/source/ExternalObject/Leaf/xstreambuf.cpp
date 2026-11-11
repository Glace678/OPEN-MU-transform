#include "stdafx.h"

#include "xstreambuf.h"

using namespace leaf;

// xstreambuf

void xstreambuf::init()
{
    m_pBuffer = NULL;
    m_offset  = 0;
    m_size = m_capacity = 0;
    m_chunksize         = 0;
}

xstreambuf::xstreambuf()
{
    init();
}
xstreambuf::xstreambuf(unsigned int size)
{
    init();
    resize(size);
}
xstreambuf::xstreambuf(const xstreambuf& xbuf)
{
    init();
    if (xbuf.size() > 0 && xbuf.data() != nullptr)
    {
        if (xbuf.size() > static_cast<size_t>((std::numeric_limits<unsigned int>::max)()))
        {
            throw std::length_error("xstreambuf copy exceeds maximum length");
        }
        write(xbuf.data(), static_cast<unsigned int>(xbuf.size()));
    }
}
xstreambuf::~xstreambuf()
{
    delete[] static_cast<BYTE*>(m_pBuffer);
}

void xstreambuf::seek(unsigned int n, XBUF_POS pos)
{
    if (pos == XBUF_BEGIN)
    {
        m_offset = n;
    }
    else if (pos == XBUF_END)
    {
        m_offset = n < size() ? static_cast<unsigned int>(size() - n) : 0;
    }
    if (m_offset > size())
        m_offset = static_cast<unsigned int>(size());
}
xstreambuf& xstreambuf::write(const void* src, unsigned int n)
{
    if (n == 0 || src == nullptr)
    {
        return *this;
    }

    if (n > (std::numeric_limits<unsigned int>::max)() - m_offset)
    {
        throw std::length_error("xstreambuf write offset overflow");
    }

    const unsigned int s = m_offset + n;
    if (size() < s)
        resize(s);

    memcpy(static_cast<BYTE*>(m_pBuffer) + m_offset, src, n);
    m_offset += n;

    return *this;
}
xstreambuf& xstreambuf::read(void* dest, unsigned int n)
{
    if (n == 0 || dest == nullptr)
    {
        return *this;
    }

    const unsigned int available =
        (m_pBuffer != nullptr && m_offset < size()) ? static_cast<unsigned int>(size() - m_offset) : 0;
    const unsigned int toCopy = (n > available) ? available : n;

    if (toCopy > 0)
    {
        memcpy(dest, static_cast<const BYTE*>(m_pBuffer) + m_offset, toCopy);
        m_offset += toCopy;
    }
    if (toCopy < n)
    {
        memset(static_cast<BYTE*>(dest) + toCopy, 0, n - toCopy);
    }

    return *this;
}

void xstreambuf::clear()
{
    delete[] static_cast<BYTE*>(m_pBuffer);

    init();
}
void xstreambuf::flush()
{
    m_offset = 0;
}

const void* xstreambuf::data() const
{
    return m_pBuffer;
}
void* xstreambuf::get_writebuf() const
{
    return m_pBuffer;
}

unsigned int xstreambuf::get_curpos() const
{
    return m_offset;
}
size_t xstreambuf::size() const
{
    return m_size;
}
void xstreambuf::resize(size_t s)
{
    if (s > static_cast<size_t>((std::numeric_limits<unsigned int>::max)()))
    {
        throw std::length_error("xstreambuf resize exceeds maximum unsigned int offset");
    }

    if (m_capacity < s)
    {
        if (s > (std::numeric_limits<size_t>::max)() - 3u)
        {
            throw std::length_error("xstreambuf resize alignment overflow");
        }
        const size_t aligned = ((s + 3u) / 4u) * 4u;
        if (m_chunksize > (std::numeric_limits<size_t>::max)() - aligned)
        {
            throw std::length_error("xstreambuf resize capacity overflow");
        }
        const size_t buf_size = m_chunksize + aligned;
        BYTE*        temp     = new BYTE[buf_size]();
        if (m_pBuffer)
        {
            const size_t cpysize = (s < size()) ? s : size();
            if (cpysize > 0)
            {
                memcpy(temp, m_pBuffer, cpysize);
            }
            delete[] static_cast<BYTE*>(m_pBuffer);
        }
        m_pBuffer  = temp;
        m_capacity = buf_size;
    }
    else if (s > m_size && m_pBuffer != nullptr)
    {
        memset(static_cast<BYTE*>(m_pBuffer) + m_size, 0, s - m_size);
    }

    m_size = s;
    if (m_offset > m_size)
        m_offset = static_cast<unsigned int>(size());
}
size_t xstreambuf::capacity() const
{
    return m_capacity;
}
bool xstreambuf::empty() const
{
    return m_size == 0;
}

void xstreambuf::set_chunksize(size_t size)
{
    m_chunksize = size;
}
size_t xstreambuf::get_chunksize()
{
    return m_chunksize;
}
