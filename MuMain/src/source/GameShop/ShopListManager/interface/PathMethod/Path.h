/*******************************************************************************
*	Author : Jin Hyejin
*	Date : 2009.06.10
*	Contents : Miscellaneous methods
*******************************************************************************/

#pragma once

class Path
{
public:

    //					Get the module's full path
    static TCHAR* GetCurrentFullPath(TCHAR* szPath);
    //					Get the module's directory
    static TCHAR* GetCurrentDirectory(TCHAR* szPath);
    //					Get the module's file name
    static TCHAR* GetCurrentFileName(TCHAR* szPath);

    //					Build a folder string: appends "\\" to the end.
    static TCHAR* SetDirString(TCHAR* szPath);
    //					Build a folder string: removes the trailing "\\"
    static TCHAR* ClearDirString(TCHAR* szPath);

    //					Build a folder string: path with the file name removed
    static TCHAR* GetDirectory(TCHAR* szPath);
    //					Build a file string: file name with the path removed
    static TCHAR* GetFileName(TCHAR* szPath);

    //					Change / to \\
    static TCHAR* ChangeSlashToBackSlash(TCHAR* szPath);
    //					Change \\ to /
    static TCHAR* ChangeBackSlashToSlash(TCHAR* szPath);

    //					Read the last line from a file
    static BOOL			ReadFileLastLine(TCHAR* szFile, TCHAR* szLastLine);
    //					Write a line to a new file
    static BOOL			WriteNewFile(TCHAR* szFile, TCHAR* szText, INT nTextSize);
    //					Create the directory for a file path
    static BOOL			CreateDirectorys(TCHAR* szFilePath, BOOL bIsFile);
};
