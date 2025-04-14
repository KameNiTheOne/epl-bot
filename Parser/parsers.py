import requests
from bs4 import BeautifulSoup
import os
import re
class PageParser:
    _exclude = ("document__insert doc-insert", "document__edit doc-edit")
    _useless = ("no-indent", "align_right no-indent")
    _testurls = ('https://www.consultant.ru//document/cons_doc_LAW_5142/171354679ab394c223ca4c93d83127aada78450e/')
    parsedUrls = set(["remove"])

    @staticmethod
    def subWithTrace(pattern, replaceTo, text: str, trace: bool) -> str:
        if trace:
            print(text)
        return re.sub(pattern, replaceTo, text)
    
    @staticmethod
    def strip(text: str, trace=False) -> str:
        text = PageParser.subWithTrace("\n[0-9].+?(?= ) ", "\n", text, trace)
        #text = re.sub('Утратил силу.*?\n', '', text)
        #text = re.sub('Абзац утрат.*?\n', '', text)
        text = PageParser.subWithTrace('\n\n+|\n([Аа]бзац утратил силу.*?)\n| \(.*?\)', '', text, trace)
        # text = PageParser.subWithTrace(r'\n([Аа]бзац утратил силу.*?)\n', '', text, trace)
        # text = PageParser.subWithTrace(' \(.*?\)', '', text, trace)
        ttext = ""
        for t in text.split('\n'):
            if re.search("Федеральный закон", t) == None:
                ttext += f"{t}\n"
        return ttext.removeprefix("\n").removesuffix("\n")
    
    @staticmethod
    def stripTitle(text: str) -> str:
        match = re.match(r".*?\d+(?:\.\d+)*", text)
        if match:
            return match.group(0)
        
    @staticmethod
    def recursive_save(nestedUrls, path):
        for l in nestedUrls:
            print(l)
            if isinstance(l[1], list):
                thing = f"{path}{l[0]}"
                try:
                    os.mkdir(thing)
                except OSError:
                    print("AAAAAAAA")
                    thing = re.sub(r'[*"<>|?]', '', thing)
                    os.mkdir(thing)
                PageParser.recursive_save(l[1], f"{path}{l[0]}\\")
            else:
                prevLen = len(PageParser.parsedUrls)
                PageParser.parsedUrls.add(l[1][1])
                curLen = len(PageParser.parsedUrls)
                if prevLen != curLen:
                    p = f"{path}{l[0]}"[:251]
                    try:
                        with open(f"{p}.txt", mode="w+", encoding="utf-8") as f:
                            f.write(f"{l[1][1]}\n{l[1][2]}\n{l[1][0]}")
                    except OSError:
                        p = re.sub(r'[*"<>|?]', '', p)
                        with open(f"{p}.txt", mode="w+", encoding="utf-8") as f:
                            f.write(f"{l[1][1]}\n{l[1][2]}\n{l[1][0]}")
                    with open("./parsed.txt", "a") as f:
                        f.write(f"{l[1][1]}\n")
                else:
                    print("\n\n\n\n\nDUPLICATE FOUND\n\n\n\n\n")
                    with open("./log.txt", "a") as f:
                        f.write(f"{l[1][1]}\n")

    @staticmethod
    def ParseFrom(response, url):
        bs = BeautifulSoup(response.text, features="html.parser")
        result = bs.find("div", "document-page__content document-page_left-padding")

        title = result.find("div", "document__style doc-style")
        if title:
            title = title.get_text(strip=False)
            title = PageParser.stripTitle(title)

            result.find("div", "document__style doc-style").decompose()
            for excl in PageParser._exclude:
                temp = result.find_all("div", excl)
                if temp:
                    for t in temp:
                        t.decompose()

            for excl in PageParser._useless:
                temp = result.find_all("p", excl)
                if temp:
                    for t in temp:
                        t.decompose()

            if url in PageParser._testurls:
                print(f"res: {PageParser.strip(result.get_text(strip=False), True)}")
            return (PageParser.strip(result.get_text(strip=False)), url, title)
        return ("123", "remove", "title")
    
    @staticmethod
    def RecursiveSearch(startUrl: str, urlText: str = ""):
        response = requests.get(startUrl)
        bs = BeautifulSoup(response.text, features="html.parser")
        print(startUrl)

        listPos = bs.find("div", "document-page__toc")
        if listPos:
            a = ""
            a.find("")
            l = listPos.find_all("a")
            thingList = []
            for item in l:
                if re.search('[Уу]трат', item.text) == None:
                    thingList.append((item.text[:15].strip(), PageParser.RecursiveSearch("https://www.consultant.ru/"+item["href"], item.text)))
            return thingList
        else:
            if re.search('[Уу]трат', urlText) == None:
                return PageParser.ParseFrom(response, startUrl)
        
    @staticmethod
    def FirstPageSearch(url: str):
        response = requests.get(url)
        bs = BeautifulSoup(response.text, features="html.parser")

        result = []
        listPos = bs.find("div", "document-page__toc").find("ul")
        if not listPos:
            result.append((chapter.text[:15].strip(), PageParser.RecursiveSearch(url)))
        else:
            for useless in listPos.find_all("ul"):
                useless.extract()
            for chapter in listPos.find_all("a"):
                result.append((chapter.text[:15].strip(), PageParser.RecursiveSearch("https://www.consultant.ru/"+chapter["href"])))
            return result