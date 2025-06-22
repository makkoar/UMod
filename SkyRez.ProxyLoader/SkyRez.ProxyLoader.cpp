// SkyRez.ProxyLoader.cpp : Определяет экспортируемые функции для DLL.
//

#include "pch.h"
#include "framework.h"
#include "SkyRez.ProxyLoader.h"


// Пример экспортированной переменной
SKYREZPROXYLOADER_API int nSkyRezProxyLoader=0;

// Пример экспортированной функции.
SKYREZPROXYLOADER_API int fnSkyRezProxyLoader(void)
{
    return 0;
}

// Конструктор для экспортированного класса.
CSkyRezProxyLoader::CSkyRezProxyLoader()
{
    return;
}
