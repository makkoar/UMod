// Приведенный ниже блок ifdef — это стандартный метод создания макросов, упрощающий процедуру
// экспорта из библиотек DLL. Все файлы данной DLL скомпилированы с использованием символа SKYREZPROXYLOADER_EXPORTS
// Символ, определенный в командной строке. Этот символ не должен быть определен в каком-либо проекте,
// использующем данную DLL. Благодаря этому любой другой проект, исходные файлы которого включают данный файл, видит
// функции SKYREZPROXYLOADER_API как импортированные из DLL, тогда как данная DLL видит символы,
// определяемые данным макросом, как экспортированные.
#ifdef SKYREZPROXYLOADER_EXPORTS
#define SKYREZPROXYLOADER_API __declspec(dllexport)
#else
#define SKYREZPROXYLOADER_API __declspec(dllimport)
#endif

// Этот класс экспортирован из библиотеки DLL
class SKYREZPROXYLOADER_API CSkyRezProxyLoader {
public:
	CSkyRezProxyLoader(void);
	// TODO: добавьте сюда свои методы.
};

extern SKYREZPROXYLOADER_API int nSkyRezProxyLoader;

SKYREZPROXYLOADER_API int fnSkyRezProxyLoader(void);
